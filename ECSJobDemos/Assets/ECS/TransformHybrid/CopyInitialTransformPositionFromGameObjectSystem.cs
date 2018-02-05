using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Transform
{
    public class CopyInitialTransformPositionFromGameObjectSystem : JobComponentSystem
    {
        struct InitialTransformGroup
        {
            [ReadOnly] public ComponentDataArray<CopyInitialTransformPositionFromGameObject> copyInitialTransformFromGameObjects;
            public TransformAccessArray transforms;
            public ComponentDataArray<Position> transformPositions;
            public EntityArray entities;
        }

        [Inject] private InitialTransformGroup m_InitialTransformGroup;
        [Inject] private DeferredEntityChangeSystem m_DeferredEntityChangeSystem;
            
        // [ComputeJobOptimization]
        struct CopyInitialTransformPositions : IJobParallelForTransform
        {
            public ComponentDataArray<Position> positions;
            public EntityArray entities;
            public NativeQueue<Entity>.Concurrent removeComponentQueue;

            public void Execute(int i, TransformAccess transform)
            {
                positions[i] = new Position
                {
                    position = transform.position
                };
                removeComponentQueue.Enqueue(entities[i]);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var copyInitialTransformPositionsJob = new CopyInitialTransformPositions
            {
                positions = m_InitialTransformGroup.transformPositions,
                entities = m_InitialTransformGroup.entities,
                removeComponentQueue = m_DeferredEntityChangeSystem.GetRemoveComponentQueue<CopyInitialTransformPositionFromGameObject>()
            };

            return copyInitialTransformPositionsJob.Schedule(m_InitialTransformGroup.transforms, inputDeps);
        }
    }
}
