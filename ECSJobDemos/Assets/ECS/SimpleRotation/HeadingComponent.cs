using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct Heading : IComponentData
    {
        public float3 value;
    }

    public class HeadingComponent : ComponentDataWrapper<Heading> { } 
}
