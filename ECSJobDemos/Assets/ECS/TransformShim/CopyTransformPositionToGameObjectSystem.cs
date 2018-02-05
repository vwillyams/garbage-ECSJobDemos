using Unity.Collections;
using Unity.Jobs;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.TransformShim
{
    public class CopyTransformPositionToGameObjectSystem : JobComponentSystem
    {
        struct PositionGroup
        {
            public ComponentDataArray<CopyTransformPositionToGameObject> copyTransformPositionToGameObjects;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public TransformAccessArray transforms;
            public int Length;
        }

        [Inject] private PositionGroup m_PositionGroup;

        [ComputeJobOptimization]
        struct PositionToMatrix : IJobParallelForTransform
        {
            [ReadOnly] public ComponentDataArray<Position> positions;

            public void Execute(int i, TransformAccess transform)
            {
                transform.position = positions[i].position;
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
