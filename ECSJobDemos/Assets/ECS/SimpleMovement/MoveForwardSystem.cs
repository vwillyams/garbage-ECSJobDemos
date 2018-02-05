using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class MoveForwardSystem : JobComponentSystem
    {
        struct MoveForwardGroup
        {
            public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<MoveForward> moveForwards;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public int Length;
        }

        [Inject] private MoveForwardGroup m_MoveForwardGroup;
    
        [ComputeJobOptimization]
        struct MoveForwardPosition : IJobParallelFor
        {
            public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                positions[i] = new Position
                {
                    position = positions[i].position + (dt * moveSpeeds[i].speed * math.forward(rotations[i].rotation))
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardPositionJob = new MoveForwardPosition();
            moveForwardPositionJob.positions = m_MoveForwardGroup.positions;
            moveForwardPositionJob.rotations = m_MoveForwardGroup.rotations;
            moveForwardPositionJob.moveSpeeds = m_MoveForwardGroup.moveSpeeds;
            moveForwardPositionJob.dt = Time.deltaTime;
            return moveForwardPositionJob.Schedule(m_MoveForwardGroup.Length, 64, inputDeps);
        } 
    }
}
