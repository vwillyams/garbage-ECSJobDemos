using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class PushablesSystem : JobComponentSystem
{
    struct Group
    {
        [ReadOnly]
        public SharedComponentDataArray<Pusher> push;
    }

    [Inject] Group m_Group; 

    [ComputeJobOptimization]
    struct PushableJob : IJobProcessComponentData<Position, Pushable>
    {
        public float dt;
        public float pushForce;
        public float3 pushOrigin;
        public bool inverse;

        public void Execute(ref Position position, [ReadOnly] ref Pushable pushable)
        {
            var distance = inverse ? pushOrigin - position.Value : position.Value - pushOrigin;
            position.Value = position.Value + (distance * pushForce * dt);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(m_Group.push.Length, Allocator.Temp);
        try
        {
            var j = 0;
            for (var i = 0; i < m_Group.push.Length; i++)
            {
                Pusher pusher = m_Group.push[i];
                if (Input.GetKey(KeyCode.Space) || pusher.alwaysActive)
                {
                    var job = new PushableJob
                    {
                        dt = Time.deltaTime,
                        pushForce = pusher.pushForce,
                        pushOrigin = pusher.position,
                        inverse = pusher.inverse
                    };
                    jobs[j] = job.Schedule(this, 64, j == 0 ? inputDeps : jobs[j - 1]);
                    j++;
                }
            }
            JobHandle allJobs = JobHandle.CombineDependencies(jobs);
            return allJobs;
        }
        finally
        {
            jobs.Dispose();
        }
    }
}

