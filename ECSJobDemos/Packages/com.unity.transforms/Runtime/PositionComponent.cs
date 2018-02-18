using System;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    [Serializable]
    public struct Position : IComponentData
    {
        public float3 position;
    }
}

namespace Unity.Transforms
{
    public class PositionComponent : ComponentDataWrapper<Position> { } 
}
