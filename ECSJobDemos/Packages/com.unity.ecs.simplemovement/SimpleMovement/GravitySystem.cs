using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleMovement
{
    public class GravitySystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        [RequireComponentTag(typeof(Gravity))]
        struct GravityPosition : IJobProcessComponentData<Position>
        {
            public void Execute(ref Position position)
            {
                position.position = position.position - new float3(0.0f, 9.8f, 0.0f);
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return new GravityPosition().Schedule(this, 64, inputDeps);
        }
    }
}
