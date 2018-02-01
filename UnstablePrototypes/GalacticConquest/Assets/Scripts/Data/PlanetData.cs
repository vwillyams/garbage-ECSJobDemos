using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace Data
{
    public struct PlanetData : IComponentData
    {
        public int TeamOwnership;
        public int Occupants;
        public Vector3 Position;
        public float Radius;
    }

    public struct RotationData : IComponentData
    {
        public float3 RotationSpeed;
    }

    public struct ShipArrivedTag : IComponentData
    {}
}
