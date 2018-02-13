using System;
using Unity.Mathematics;
using UnityEngine.ECS.SimpleSpatialQuery;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestTargetPosition : IComponentData, INearestTarget
    {
        public float3 value { get; set; }

        public Type TargetType()
        {
            return typeof(BoidTarget);
        }
    }

    public class BoidNearestTargetPositionComponent : ComponentDataWrapper<BoidNearestTargetPosition> { }
}
