using Unity.ECS;
using UnityEngine;

namespace Data
{
    public struct ShipData : IComponentData
    {
        public Entity TargetEntity;
        public int TeamOwnership;
    }
}
