using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class GravitySystem : JobComponentSystem
    {
        struct GravityGroup
        {
            public ComponentDataArray<Position> positions;
            [ReadOnly]
            public ComponentDataArray<Gravity> gravity;
            public int Length;
        }

        [Inject] private GravityGroup m_GravityGroup;
    
        [ComputeJobOptimization]
        struct GravityPosition : IJobParallelFor
        {
            public ComponentDataArray<Position> positions;
            [ReadOnly]
            public ComponentDataArray<Gravity> gravity;
        
            public void Execute(int i)
            {
                positions[i] = new Position
                {
                    position = positions[i].position - new float3(0.0f, 9.8f, 0.0f)
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var gravityPositionJob = new GravityPosition();
            gravityPositionJob.positions = m_GravityGroup.positions;
            gravityPositionJob.gravity = m_GravityGroup.gravity;
            return gravityPositionJob.Schedule(m_GravityGroup.Length, 64, inputDeps);
        } 
    }
}
