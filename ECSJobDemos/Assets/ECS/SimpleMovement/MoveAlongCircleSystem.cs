using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class MoveAlongCircleSystem : JobComponentSystem
    {
        struct MoveAlongCircleGroup
        {
            public ComponentDataArray<Position> positions;
            public ComponentDataArray<MoveAlongCircle> moveAlongCircles;
            [ReadOnly]
            public ComponentDataArray<MoveSpeed> moveSpeeds;
            public int Length;
        }

        [Inject] private MoveAlongCircleGroup m_MoveAlongCircleGroup;
    
        [ComputeJobOptimization]
        struct MoveAlongCirclePosition : IJobParallelFor
        {
            public ComponentDataArray<Position> positions;
            public ComponentDataArray<MoveAlongCircle> moveAlongCircles;
            [ReadOnly]
            public ComponentDataArray<MoveSpeed> moveSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                float t = moveAlongCircles[i].t + (dt * moveSpeeds[i].speed);
                float x = moveAlongCircles[i].center.x + (math.cos(t) * moveAlongCircles[i].radius);
                float y = moveAlongCircles[i].center.y;
                float z = moveAlongCircles[i].center.z + (math.sin(t) * moveAlongCircles[i].radius);

                moveAlongCircles[i] = new MoveAlongCircle
                {
                    t = t,
                    center = moveAlongCircles[i].center,
                    radius = moveAlongCircles[i].radius
                };
                
                positions[i] = new Position
                {
                    position = new float3(x,y,z)
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveAlongCirclePositionJob = new MoveAlongCirclePosition();
            moveAlongCirclePositionJob.positions = m_MoveAlongCircleGroup.positions;
            moveAlongCirclePositionJob.moveAlongCircles = m_MoveAlongCircleGroup.moveAlongCircles;
            moveAlongCirclePositionJob.moveSpeeds = m_MoveAlongCircleGroup.moveSpeeds;
            moveAlongCirclePositionJob.dt = Time.deltaTime;
            return moveAlongCirclePositionJob.Schedule(m_MoveAlongCircleGroup.Length, 64, inputDeps);
        } 
    }
}
