using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class GravitySystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct GravityPosition : IJobParallelFor
        {
            public ComponentDataArray<Position> positions;
        
            public void Execute(int i)
            {
                positions[i] = new Position { position = positions[i].position - new float3(0.0f, 9.8f, 0.0f) };
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var gravityGroup = EntityManager.CreateComponentGroup(ComponentType.ReadOnly(typeof(Gravity)), typeof(Position));
            var positions = gravityGroup.GetComponentDataArray<Position>();

            var gravityPositionJob = new GravityPosition();
            gravityPositionJob.positions = positions;
            // Nothing is injected so inputDeps is not used
            var gravityPosJobHandle = gravityPositionJob.Schedule(positions.Length, 64, gravityGroup.GetDependency());

            gravityGroup.AddDependency(gravityPosJobHandle);

            gravityGroup.Dispose();

            // Nothing is injected so the return value is not used
            return inputDeps;
        }
    }
}
