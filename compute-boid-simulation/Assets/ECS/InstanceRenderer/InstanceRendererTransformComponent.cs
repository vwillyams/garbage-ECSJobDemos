﻿using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Rendering
{
    public struct InstanceRendererTransform : IComponentData
    {
        public float4x4 matrix;
    }

    public class InstanceRendererTransformComponent : ComponentDataWrapper<InstanceRendererTransform> { }
}