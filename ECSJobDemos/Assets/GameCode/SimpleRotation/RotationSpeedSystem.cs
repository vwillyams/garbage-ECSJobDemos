using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RotationSpeedSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct RotationSpeedRotation : IJobProcessComponentData<Rotation, RotationSpeed>
        {
            public float dt;

            public void Execute(ref Rotation rotation, [ReadOnly]ref RotationSpeed speed)
            {
                rotation.value = math.mul(math.normalize(rotation.value), math.axisAngle(math.up(), speed.speed * dt));
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new RotationSpeedRotation() { dt = Time.deltaTime };
            return job.Schedule(this, 64, inputDeps);
        } 
    }
}
