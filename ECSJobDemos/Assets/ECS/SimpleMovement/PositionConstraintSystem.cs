using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class PositionConstraintSystem : JobComponentSystem
    {
        struct PositionConstraintsGroup
        {
            [ReadOnly]
            public ComponentDataArray<PositionConstraint> positionConstraints;
            [ReadOnly]
            public EntityArray entities;
            public int Length;
        }

        [Inject] private PositionConstraintsGroup m_PositionContraintsGroup;

        struct PositionsGroup
        {
            public ComponentDataArray<TransformPosition> positions;
            [ReadOnly]
            public EntityArray entities;
            public int Length;
        }

        [Inject] private PositionsGroup m_PositionsGroup;

        [ComputeJobOptimization]
        struct ContrainPositions : IJob
        {
            public ComponentDataArray<TransformPosition> positions;
            [ReadOnly]
            public EntityArray entities;
            [ReadOnly]
            public ComponentDataArray<PositionConstraint> positionConstraints;
            [ReadOnly]
            public EntityArray positionConstraintEntities;
            [ReadOnly]
            public NativeHashMap<Entity, int> entityIndexHashMap;

            public void Execute()
            {
                for (int i = 0; i < positionConstraints.Length; i++)
                {
                    int childIndex = 0;
                    int parentIndex = 0;

                    entityIndexHashMap.TryGetValue(positionConstraintEntities[i], out childIndex);
                    entityIndexHashMap.TryGetValue(positionConstraints[i].parentEntity, out parentIndex);

                    var childPosition = positions[childIndex].position;
                    var parentPosition = positions[parentIndex].position;

                    float3 d = childPosition - parentPosition;
                    float len = math.length(d);
                    float nl = math.min(math.max(len, positionConstraints[i].minDistance),
                        positionConstraints[i].maxDistance);

                    positions[childIndex] = new TransformPosition
                    {
                        position = parentPosition + ((d * nl) / len)
                    };
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (EntityManager.GetComponentOrderVersion<TransformPosition>() != m_PositionOrderVersion)
            {
                m_EntityIndexHashMap?.Dispose();
                var entityIndexHashMap = new NativeHashMap<Entity, int>(m_PositionsGroup.entities.Length, Allocator.Persistent);
                for (int i = 0; i < m_PositionsGroup.entities.Length; i++)
                {
                    entityIndexHashMap.TryAdd(m_PositionsGroup.entities[i], i);
                }
                m_EntityIndexHashMap = entityIndexHashMap;
                m_PositionOrderVersion = EntityManager.GetComponentOrderVersion<TransformPosition>();
            }
            var constrainPositionsJob = new ContrainPositions();
            constrainPositionsJob.entities = m_PositionsGroup.entities;
            constrainPositionsJob.positions = m_PositionsGroup.positions;
            constrainPositionsJob.positionConstraints = m_PositionContraintsGroup.positionConstraints;
            constrainPositionsJob.positionConstraintEntities = m_PositionContraintsGroup.entities;
            constrainPositionsJob.entityIndexHashMap = m_EntityIndexHashMap.Value;
            return constrainPositionsJob.Schedule(inputDeps);
        }

        NativeHashMap<Entity, int>? m_EntityIndexHashMap;
        int m_PositionOrderVersion;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_PositionOrderVersion = -1;
            m_EntityIndexHashMap = null;
        }

        protected override void OnDestroyManager()
        {
            base.OnDestroyManager();
            m_EntityIndexHashMap?.Dispose();
        }
    }
}
