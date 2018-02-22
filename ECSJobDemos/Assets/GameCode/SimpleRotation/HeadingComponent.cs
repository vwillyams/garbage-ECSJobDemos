using System;
using Unity.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct Heading : IComponentData, ISingleValue<float3>
    {
        public float3 Value { get; set; }
    }

    public class HeadingComponent : ComponentDataWrapper<Heading> { } 
}
