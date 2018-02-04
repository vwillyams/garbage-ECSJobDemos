using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleTime;

namespace UnityEngine.ECS.SimpleTime
{
    public class UpdateTimeSystem : JobComponentSystem
    {
        struct UpdateTimeGroup
        {
            public ComponentDataArray<UpdateTime> updateTime;
            public int Length;
        }
        
        [Inject] private UpdateTimeGroup m_UpdateTimeGroup;
    
        [ComputeJobOptimization]
        struct UpdateTimeChange: IJobParallelFor
        {
            public ComponentDataArray<UpdateTime> updateTime;
            public float dt;
        
            public void Execute(int i)
            {
                updateTime[i] = new UpdateTime
                {
                    t = updateTime[i].t + dt 
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var updateTimeChangeJob = new UpdateTimeChange();
            updateTimeChangeJob.updateTime = m_UpdateTimeGroup.updateTime;
            updateTimeChangeJob.dt = Time.deltaTime;
            return updateTimeChangeJob.Schedule(m_UpdateTimeGroup.Length, 64, inputDeps);
        } 
    }
}
