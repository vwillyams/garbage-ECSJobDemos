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
        [ComputeJobOptimization]
        struct MoveForwardPosition : IJobParallelFor
        {
            public ComponentDataArray<Position2D> positions;
            [ReadOnly] public ComponentDataArray<Heading2D> headings;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                positions[i] = new Position2D { position = positions[i].position + (dt * moveSpeeds[i].speed * headings[i].heading) };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardGroup = EntityManager.CreateComponentGroup(
                ComponentType.ReadOnly(typeof(MoveForward)),
                ComponentType.ReadOnly(typeof(Heading2D)),
                ComponentType.ReadOnly(typeof(MoveSpeed)),
                typeof(Position2D));
            
            var positions  = moveForwardGroup.GetComponentDataArray<Position2D>();
            var headings   = moveForwardGroup.GetComponentDataArray<Heading2D>();
            var moveSpeeds = moveForwardGroup.GetComponentDataArray<MoveSpeed>();

            var moveForwardPositionJob = new MoveForwardPosition
            {
                positions = positions,
                headings = headings,
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
