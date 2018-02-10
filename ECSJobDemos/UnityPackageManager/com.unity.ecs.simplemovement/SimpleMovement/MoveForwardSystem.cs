using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class MoveForwardSystem : JobComponentSystem
    {
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
                    position = positions[i].position + (dt * moveSpeeds[i].speed * math.forward(rotations[i].value))
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardGroup = EntityManager.CreateComponentGroup(
                ComponentType.ReadOnly(typeof(MoveForward)),
                ComponentType.ReadOnly(typeof(Rotation)),
                ComponentType.Subtractive(typeof(LocalRotation)),
                ComponentType.ReadOnly(typeof(MoveSpeed)),
                typeof(Position));
            
            var positions = moveForwardGroup.GetComponentDataArray<Position>();
            var rotations = moveForwardGroup.GetComponentDataArray<Rotation>();
            var moveSpeeds = moveForwardGroup.GetComponentDataArray<MoveSpeed>();

            var moveForwardPositionJob = new MoveForwardPosition
            {
                positions = positions,
                rotations = rotations,
                moveSpeeds = moveSpeeds,
                dt = Time.deltaTime
            };
            
            var moveForwardPositionJobHandle = moveForwardPositionJob.Schedule(positions.Length, 64, inputDeps);
            
            moveForwardGroup.AddDependency(moveForwardPositionJobHandle);
            
            var moveForwardGroupJobHandle = moveForwardGroup.GetDependency();
            moveForwardGroup.Dispose();

            return moveForwardGroupJobHandle;
        } 
    }
}
