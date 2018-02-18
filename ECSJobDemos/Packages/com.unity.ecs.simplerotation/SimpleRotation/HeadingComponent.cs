using System;
using Unity.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct Heading : IComponentData
    {
        public float3 value;
    }

    public class HeadingComponent : ComponentDataWrapper<Heading> { } 
}
