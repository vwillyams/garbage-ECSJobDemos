using System;
using Unity.Mathematics;
using UnityEngine.ECS.SimpleSpatialQuery;

namespace UnityEngine.ECS.Boids
{
    public struct BoidNearestObstaclePosition : IComponentData, INearestTarget
    {
        public float3 value { get; set; }

        public Type TargetType()
        {
            return typeof(BoidObstacle);
        }
    }

    public class BoidNearestObstaclePositionComponent : ComponentDataWrapper<BoidNearestObstaclePosition> { }
}
