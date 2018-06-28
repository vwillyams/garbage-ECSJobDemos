using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class MyRotationSpeedSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct RotationSpeedJob : IJobProcessComponentData<Rotation, RotationSpeed>
    {
        public float dt; 

        public void Execute(ref Rotation rotation, [ReadOnly] ref RotationSpeed rotationSpeed)
        {
            rotation.Value = math.mul(math.normalize(rotation.Value), math.axisAngle(math.up(), rotationSpeed.Value * dt));
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var job = new RotationSpeedJob() { dt = Time.deltaTime };
        return job.Schedule(this, 64, inputDeps);
    }
}

