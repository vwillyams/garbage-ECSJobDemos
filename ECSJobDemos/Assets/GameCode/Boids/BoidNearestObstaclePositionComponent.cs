using Unity.ECS;
using Unity.Mathematics;
using UnityEngine.ECS.SimpleSpatialQuery;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestObstaclePosition : IComponentData, ISingleValue<float3>
    {
        public float3 Value { get; set; }
    }

    public class BoidNearestObstaclePositionComponent : ComponentDataWrapper<BoidNearestObstaclePosition> { }
    public class BoidNearestObstaclePositionSystem : NearestTargetPositionSystem<BoidNearestObstaclePosition, BoidObstacle> { }
}
