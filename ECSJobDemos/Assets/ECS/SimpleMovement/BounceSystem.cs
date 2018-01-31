using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class BounceSystem : JobComponentSystem
    {
        struct BounceGroup
        {
            public ComponentDataArray<TransformPosition> positions;
            public ComponentDataArray<Bounce> bounce;
            public int Length;
        }
        

        [Inject] private BounceGroup m_BounceGroup;
    
        [ComputeJobOptimization]
        struct BouncePosition : IJobParallelFor
        {
            public ComponentDataArray<TransformPosition> positions;
            public ComponentDataArray<Bounce> bounce;
            public float dt;
        
            public void Execute(int i)
            {
                float t = bounce[i].t + (i*0.005f);
                float st = math.sin(t);
                float3 prevPosition = positions[i].position;
                Bounce prevBounce = bounce[i];
                
                positions[i] = new TransformPosition
                {
                    position = prevPosition + new float3( st*prevBounce.height.x, st*prevBounce.height.y, st*prevBounce.height.z )
                };

                bounce[i] = new Bounce
                {
                    t = prevBounce.t + (dt * prevBounce.speed),
                    height = prevBounce.height,
                    speed = prevBounce.speed
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var bouncePositionJob = new BouncePosition();
            bouncePositionJob.positions = m_BounceGroup.positions;
            bouncePositionJob.bounce = m_BounceGroup.bounce;
            bouncePositionJob.dt = Time.deltaTime;
            return bouncePositionJob.Schedule(m_BounceGroup.Length, 64, inputDeps);
        } 
    }
}
