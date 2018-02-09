using Unity.Collections;
using Unity.Jobs;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.TransformShim
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

        [Inject] private RotationGroup m_RotationGroup;

        [ComputeJobOptimization]
        struct RotationToMatrix : IJobParallelForTransform
        {
            [ReadOnly] public ComponentDataArray<Rotation> rotations;

            public void Execute(int i, TransformAccess transform)
            {
                transform.rotation = new Quaternion
                {
                    x = rotations[i].value.value.x,
                    y = rotations[i].value.value.y,
                    z = rotations[i].value.value.z,
                    w = rotations[i].value.value.w
                };
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
