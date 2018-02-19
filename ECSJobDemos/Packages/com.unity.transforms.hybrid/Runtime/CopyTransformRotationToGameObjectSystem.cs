using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Unity.Transforms.Hybrid
{
    public class CopyTransformRotationToGameObjectSystem : JobComponentSystem
    {
        struct RotationGroup
        {
            public ComponentDataArray<CopyTransformRotationToGameObject> copyTransformRotationToGameObjects;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            public TransformAccessArray transforms;
            public int Length;
        }

        [Inject] RotationGroup m_RotationGroup;

        [ComputeJobOptimization]
        struct RotationToMatrix : IJobParallelForTransform
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;

            public void Execute(int i, TransformAccess transform)
            {
                transform.rotation = rotations[i].Value;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var worldRotationToMatrixJob = new RotationToMatrix();
            worldRotationToMatrixJob.rotations = m_RotationGroup.rotations;
            return worldRotationToMatrixJob.Schedule(m_RotationGroup.transforms, inputDeps);
        }
    }
}
