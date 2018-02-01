using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace Data
{
    public struct ShipData : IComponentData
    {
        public Entity TargetEntity;

        public int TeamOwnership;
    }
}
