using System;
using Unity.ECS;
using Unity.Mathematics;
using UnityEngine.ECS.SimpleSpatialQuery;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestTargetPosition : IComponentData, INearestTarget
    {
        public float3 value { get; set; }
    }

    public class BoidNearestTargetPositionComponent : ComponentDataWrapper<BoidNearestTargetPosition> { }
    public class BoidNearestTargetPositionSystem : NearestTargetPositionSystem<BoidNearestTargetPosition, BoidTarget> { }
}
