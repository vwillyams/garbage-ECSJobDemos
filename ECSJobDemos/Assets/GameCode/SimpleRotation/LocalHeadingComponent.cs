﻿using System;
using Unity.Entities;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct LocalHeading : IComponentData
    {
        public float3 Value { get; set; }
    }

    public class LocalHeadingComponent : ComponentDataWrapper<LocalHeading> { } 
}
