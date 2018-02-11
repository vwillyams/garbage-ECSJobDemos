using Unity.Mathematics;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestTargetPosition : IComponentData
    {
        public float3 value;
    }

    public class BoidNearestTargetPositionComponent : ComponentDataWrapper<BoidNearestTargetPosition> { }
}
