using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RotationAccelerationSystem : JobComponentSystem
    {
        struct RotationAccelerationGroup
        {
            [ReadOnly] public ComponentDataArray<RotationAcceleration> rotationAccelerations;
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            public int Length;
        }

        [Inject] private RotationAccelerationGroup m_RotationAccelerationGroup;
    
        [ComputeJobOptimization]
        struct RotationSpeedAcceleration : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<RotationAcceleration> rotationAccelerations;
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                rotationSpeeds[i] = new RotationSpeed
                {
                    speed = math.max(0.0f,rotationSpeeds[i].speed+(rotationAccelerations[i].speed*dt))
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationSpeedAccelerationJob = new RotationSpeedAcceleration
            {
                rotationAccelerations = m_RotationAccelerationGroup.rotationAccelerations,
                rotationSpeeds = m_RotationAccelerationGroup.rotationSpeeds,
                dt = Time.deltaTime
            };
            return rotationSpeedAccelerationJob.Schedule(m_RotationAccelerationGroup.Length, 64, inputDeps);
        } 
    }
}
