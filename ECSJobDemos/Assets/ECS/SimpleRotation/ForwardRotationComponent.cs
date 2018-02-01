﻿using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct ForwardRotation : IComponentData
    {
        public float3 forward;
    }

    public class ForwardRotationComponent : ComponentDataWrapper<ForwardRotation> { } 
}
