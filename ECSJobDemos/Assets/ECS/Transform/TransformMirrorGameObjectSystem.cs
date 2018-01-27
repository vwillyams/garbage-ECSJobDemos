using Unity.Jobs;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Transform
{
    public class TransformMirrorGameObjectSystem : JobComponentSystem
    {
        struct WorldPositionGroup
        {
            public ComponentDataArray<TransformPosition> positions;
            public TransformAccessArray transforms;
            public int Length;
        }

        [Inject] private WorldPositionGroup m_WorldPositionGroup;

        [ComputeJobOptimization]
        struct WorldPositionToMatrix : IJobParallelForTransform
        {
            public ComponentDataArray<TransformPosition> positions;

            public void Execute(int i, TransformAccess transform)
            {
                var transformPosition = new TransformPosition();
                var position = transform.position;
                transformPosition.position = position;
                positions[i] = transformPosition;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var worldPositionToMatrixJob = new WorldPositionToMatrix();
            worldPositionToMatrixJob.positions = m_WorldPositionGroup.positions;
            return worldPositionToMatrixJob.Schedule(m_WorldPositionGroup.transforms, inputDeps);
        }
    }
}
