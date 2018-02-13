using System;
using Unity.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Transform2D
{
    [Serializable]
    public struct Heading2D : IComponentData
    {
        public float2 heading;
    }

    public class Heading2DComponent : ComponentDataWrapper<Heading2D> { } 
}
