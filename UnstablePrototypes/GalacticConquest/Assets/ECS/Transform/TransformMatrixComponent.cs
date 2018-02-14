using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Transform
{
    public struct TransformMatrix : IComponentData
    {
        public float4x4 matrix;
    }

    public class TransformMatrixComponent : ComponentDataWrapper<TransformMatrix> { }
}