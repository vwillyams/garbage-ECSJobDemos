using System;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Boids
{
    [Serializable]
    public struct BoidSettings : IComponentData
    {
        public float cellRadius;
        public float separationWeight;
        public float alignmentWeight;
        public float targetWeight;
        public float obstacleWeight;
        public float obstacleAversionDistance;
        public float rotationalSpeed;
    }
    public class BoidSettingsComponent : ComponentDataWrapper<BoidSettings> { }
}
