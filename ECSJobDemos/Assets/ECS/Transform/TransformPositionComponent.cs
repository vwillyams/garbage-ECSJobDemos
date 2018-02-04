using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    [System.Serializable]
    public struct TransformPosition : IComponentData
    {
        public float3 position;
    }

    public class TransformPositionComponent : ComponentDataWrapper<TransformPosition> { } 
}
