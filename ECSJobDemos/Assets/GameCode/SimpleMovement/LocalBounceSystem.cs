﻿using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace UnityEngine.ECS.SimpleMovement
{
    public class LocalBounceSystem : JobComponentSystem
    {
        struct LocalBounceGroup
        {
            public ComponentDataArray<LocalPosition> positions;
            public ComponentDataArray<LocalBounce> bounce;
            public int Length;
        }
        

        [Inject] private LocalBounceGroup m_LocalBounceGroup;
    
        [ComputeJobOptimization]
        struct LocalBounceLocalPosition : IJobParallelFor
        {
            public ComponentDataArray<LocalPosition> positions;
            public ComponentDataArray<LocalBounce> bounce;
            public float dt;
        
            public void Execute(int i)
            {
                float t = bounce[i].t + (i*0.005f);
                float st = math.sin(t);
                float3 prevLocalPosition = positions[i].Value;
                LocalBounce prevLocalBounce = bounce[i];
                
                positions[i] = new LocalPosition
                {
                    Value = prevLocalPosition + new float3( st*prevLocalBounce.height.x, st*prevLocalBounce.height.y, st*prevLocalBounce.height.z )
                };

                bounce[i] = new LocalBounce
                {
                    t = prevLocalBounce.t + (dt * prevLocalBounce.speed),
                    height = prevLocalBounce.height,
                    speed = prevLocalBounce.speed
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var bounceLocalPositionJob = new LocalBounceLocalPosition();
            bounceLocalPositionJob.positions = m_LocalBounceGroup.positions;
            bounceLocalPositionJob.bounce = m_LocalBounceGroup.bounce;
            bounceLocalPositionJob.dt = Time.deltaTime;
            return bounceLocalPositionJob.Schedule(m_LocalBounceGroup.Length, 64, inputDeps);
        } 
    }
}