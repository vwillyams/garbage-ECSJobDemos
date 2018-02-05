using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform2D
{
    [Serializable]
    public struct Heading2D : IComponentData
    {
        public float2 heading;
    }

    public class Heading2DComponent : ComponentDataWrapper<Heading2D> { } 
}
