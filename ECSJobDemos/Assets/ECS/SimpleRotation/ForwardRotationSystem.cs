using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class ForwardRotationSystem : JobComponentSystem
    {
        struct ForwardRotationGroup
        {
            public ComponentDataArray<TransformRotation> rotations;
            [ReadOnly]
            public ComponentDataArray<ForwardRotation> forwardRotations;
            public int Length;
        }

        [Inject] private ForwardRotationGroup m_ForwardRotationGroup;
    
        [ComputeJobOptimization]
        struct ForwardRotationRotation : IJobParallelFor
        {
            public ComponentDataArray<TransformRotation> rotations;
            [ReadOnly]
            public ComponentDataArray<ForwardRotation> forwardRotations;
        
            public void Execute(int i)
            {
                rotations[i] = new TransformRotation
                {
                    rotation = math.lookRotationToQuaternion(forwardRotations[i].forward, math.up())
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var forwardRotationRotationJob = new ForwardRotationRotation
            {
                rotations = m_ForwardRotationGroup.rotations,
                forwardRotations = m_ForwardRotationGroup.forwardRotations,
            };
            return forwardRotationRotationJob.Schedule(m_ForwardRotationGroup.Length, 64, inputDeps);
        } 
    }
}
