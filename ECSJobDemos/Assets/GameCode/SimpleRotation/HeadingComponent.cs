using System;
using Unity.Entities;
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
