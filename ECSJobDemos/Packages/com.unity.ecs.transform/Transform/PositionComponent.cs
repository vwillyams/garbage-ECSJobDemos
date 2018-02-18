using System;
using Unity.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Transform
{
    [Serializable]
    public struct Position : IComponentData
    {
        public float3 position;
    }

    public class PositionComponent : ComponentDataWrapper<Position> { } 
}
