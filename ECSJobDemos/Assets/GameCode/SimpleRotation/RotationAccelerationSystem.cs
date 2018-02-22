using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleRotation
{
    [DisableSystemWhenEmpty]
    public class RotationAccelerationSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct RotationSpeedAcceleration : IJobProcessComponentData<RotationSpeed, RotationAcceleration>
        {
            public float dt;
        
            public void Execute(ref RotationSpeed speed, [ReadOnly]ref RotationAcceleration acceleration)
            {
                speed.speed = math.max(0.0f, speed.speed + (acceleration.speed * dt));
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationSpeedAccelerationJob = new RotationSpeedAcceleration { dt = Time.deltaTime };
            return rotationSpeedAccelerationJob.Schedule(this, 64, inputDeps);
        } 
    }
}