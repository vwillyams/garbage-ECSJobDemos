using System;
using Unity.ECS;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    [Serializable]
    public struct Boid : ISharedComponentData
    {
        public float cellRadius;
        public float separationWeight;
        public float alignmentWeight;
        public float targetWeight;
        public float obstacleWeight;
        public float obstacleAversionDistance;
        public float rotationalSpeed; 
    }

    public class BoidComponent : SharedComponentDataWrapper<Boid> { }
    

}
