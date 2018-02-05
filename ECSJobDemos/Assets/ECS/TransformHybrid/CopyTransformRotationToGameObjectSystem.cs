using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Transform
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
                    x = rotations[i].rotation.value.x,
                    y = rotations[i].rotation.value.y,
                    z = rotations[i].rotation.value.z,
                    w = rotations[i].rotation.value.w
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
