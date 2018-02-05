using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform;
using UnityEngine.ECS.Transform2D;

namespace UnityEngine.ECS.SimpleMovement2D
{
    public class MoveForward2DSystem : JobComponentSystem
    {
        struct MoveForwardGroup
        {
            public ComponentDataArray<Position2D> positions;
            [ReadOnly] public ComponentDataArray<Heading2D> headings;
            [ReadOnly] public ComponentDataArray<MoveForward> moveForwards;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public int Length;
        }

        [Inject] private MoveForwardGroup m_MoveForwardGroup;
    
        [ComputeJobOptimization]
        struct MoveForwardPosition2D : IJobParallelFor
        {
            public ComponentDataArray<Position2D> positions;
            [ReadOnly] public ComponentDataArray<Heading2D> headings;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                positions[i] = new Position2D
                {
                    position = positions[i].position + (dt * moveSpeeds[i].speed * headings[i].heading)
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardPosition2DJob = new MoveForwardPosition2D();
            moveForwardPosition2DJob.positions = m_MoveForwardGroup.positions;
            moveForwardPosition2DJob.headings = m_MoveForwardGroup.headings;
            moveForwardPosition2DJob.moveSpeeds = m_MoveForwardGroup.moveSpeeds;
            moveForwardPosition2DJob.dt = Time.deltaTime;
            return moveForwardPosition2DJob.Schedule(m_MoveForwardGroup.Length, 64, inputDeps);
        } 
    }
}
