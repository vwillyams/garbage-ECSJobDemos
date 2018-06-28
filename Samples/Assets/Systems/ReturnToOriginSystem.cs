using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ReturnToOriginSystem : JobComponentSystem
{
    [ComputeJobOptimization]
    struct ReturnToOriginJob : IJobProcessComponentData<Position, ReturnToOrigin>
    {
        public float dt; 

        public void Execute(ref Position position, [ReadOnly] ref ReturnToOrigin returnToOrigin)
        {
            var heading = returnToOrigin.origin - position.Value;
            var distance = math.distance(returnToOrigin.origin, position.Value);
            var TOLERANCE = 0.5f;
            if (distance > TOLERANCE)
            {
                var direction = heading / distance;
                var positionChange = direction * returnToOrigin.returnForce * dt;
                position.Value = position.Value + direction * returnToOrigin.returnForce * dt;
            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var job = new ReturnToOriginJob() { dt = Time.deltaTime };
        return job.Schedule(this, 64, inputDeps);
    }
}

