using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RotationSpeedSlowdownSystem : JobComponentSystem
    {
        struct RotationSpeedSlowdownGroup
        {
            [ReadOnly]
            public ComponentDataArray<RotationSpeedSlowdown> rotationSpeedSlowdowns;
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            public int Length;
        }

        [Inject] private RotationSpeedSlowdownGroup m_RotationSpeedSlowdownGroup;
    
        [ComputeJobOptimization]
        struct RotationSpeedSlowdownRotation : IJobParallelFor
        {
            [ReadOnly]
            public ComponentDataArray<RotationSpeedSlowdown> rotationSpeedSlowdowns;
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                rotationSpeeds[i] = new RotationSpeed
                {
                    speed = math.max(0.0f,rotationSpeeds[i].speed-(rotationSpeedSlowdowns[i].speed*dt))
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationSpeedSlowdownRotationJob = new RotationSpeedSlowdownRotation
            {
                rotationSpeedSlowdowns = m_RotationSpeedSlowdownGroup.rotationSpeedSlowdowns,
                rotationSpeeds = m_RotationSpeedSlowdownGroup.rotationSpeeds,
                dt = Time.deltaTime
            };
            return rotationSpeedSlowdownRotationJob.Schedule(m_RotationSpeedSlowdownGroup.Length, 64, inputDeps);
        } 
    }
}
