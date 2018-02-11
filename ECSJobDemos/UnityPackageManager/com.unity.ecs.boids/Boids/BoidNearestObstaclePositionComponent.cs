using Unity.Mathematics;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestObstaclePosition : IComponentData
    {
        public float3 value;
    }

    public class BoidNearestObstaclePositionComponent : ComponentDataWrapper<BoidNearestObstaclePosition> { }
}
