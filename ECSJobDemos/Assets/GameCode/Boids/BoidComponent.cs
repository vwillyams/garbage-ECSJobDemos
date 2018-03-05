using System;
using Unity.Entities;

namespace UnityEngine.ECS.Boids
{
    [Serializable]
    public struct Boid : ISharedComponentData
    {
        public float cellRadius;
        public float separationWeight;
        public float alignmentWeight;
        public float targetWeight;
        public float obstacleAversionDistance;
        public float speed;
    }

    public class BoidComponent : SharedComponentDataWrapper<Boid> { }
}
