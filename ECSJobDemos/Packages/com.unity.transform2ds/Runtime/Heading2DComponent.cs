using System;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms2D
{
    [Serializable]
    public struct Heading2D : IComponentData
    {
        public float2 Heading;
    }

    public class Heading2DComponent : ComponentDataWrapper<Heading2D> { }
}
