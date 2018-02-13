﻿using System;
using Unity.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct LocalHeading : IComponentData
    {
        public float3 value;
    }

    public class LocalHeadingComponent : ComponentDataWrapper<LocalHeading> { } 
}
