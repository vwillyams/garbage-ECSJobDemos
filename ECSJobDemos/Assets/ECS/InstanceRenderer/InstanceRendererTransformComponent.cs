using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Rendering
{
    public struct InstanceRendererTransform : IComponentData
    {
        public float4x4 matrix;
    }

    public class InstanceRendererTransformComponent : ComponentDataWrapper<InstanceRendererTransform> { }
}