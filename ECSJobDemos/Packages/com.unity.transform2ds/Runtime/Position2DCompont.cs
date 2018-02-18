using System;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms2D
{
    [Serializable]
    public struct Position2D : IComponentData
    {
        public float2 position;
    }

    public class Position2DComponent : ComponentDataWrapper<Position2D> { } 
}
