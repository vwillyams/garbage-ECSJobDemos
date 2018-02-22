using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace Unity.Transforms.Hybrid
{
    public class CopyTransformPositionFromGameObjectSystem : JobComponentSystem
    {
        struct PositionGroup
        {
            public ComponentDataArray<CopyTransformPositionFromGameObject> copyTransformPositionFromGameObjects;
            public ComponentDataArray<Position> positions;
            public TransformAccessArray transforms;
            public int Length;
        }

        [Inject] PositionGroup m_PositionGroup;

        [ComputeJobOptimization]
        struct PositionToMatrix : IJobParallelForTransform
        {
            public ComponentDataArray<Position> positions;

            public void Execute(int i, TransformAccess transform)
            {
                positions[i] = new Position
                {
                    Value = transform.position
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var worldPositionToMatrixJob = new PositionToMatrix();
            worldPositionToMatrixJob.positions = m_PositionGroup.positions;
            return worldPositionToMatrixJob.Schedule(m_PositionGroup.transforms, inputDeps);
        }
    }
}
