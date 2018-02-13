using System;
using UnityEngine;
using Unity.Collections;
using Unity.ECS;
using Unity.Mathematics;

namespace Unity.ECS.Rendering
{
    public struct InstanceRendererTransform : IComponentData
    {
        public float4x4 matrix;
    }

    public class InstanceRendererTransformComponent : ComponentDataWrapper<InstanceRendererTransform> { }
}