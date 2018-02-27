using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.ECS.SimpleSpatialQuery;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestTargetPosition : IComponentData, ISingleValue<float3>
    {
        public float3 Value { get; set; }
    }

    public class BoidNearestTargetPositionComponent : ComponentDataWrapper<BoidNearestTargetPosition> { }
    public class BoidNearestTargetPositionSystem : NearestTargetPositionSystem<BoidNearestTargetPosition, BoidTarget> { }
}
