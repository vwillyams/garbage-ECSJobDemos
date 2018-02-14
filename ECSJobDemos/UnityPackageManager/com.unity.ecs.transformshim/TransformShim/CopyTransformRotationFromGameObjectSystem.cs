using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.TransformShim
{
    public class CopyTransformRotationFromGameObjectSystem : JobComponentSystem
    {
        struct RotationGroup
        {
            public ComponentDataArray<CopyTransformRotationFromGameObject> copyTransformRotationFromGameObjects;
            public ComponentDataArray<Rotation> rotations;
            public TransformAccessArray transforms;
            public int Length;
        }

        [Inject] private RotationGroup m_RotationGroup;

        [ComputeJobOptimization]
        struct RotationToMatrix : IJobParallelForTransform
        {
            public ComponentDataArray<Rotation> rotations;

            public void Execute(int i, TransformAccess transform)
            {
                rotations[i] = new Rotation
                {
                    value = new quaternion(
                        transform.rotation.x, 
                        transform.rotation.y, 
                        transform.rotation.z, 
                        transform.rotation.w )
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
