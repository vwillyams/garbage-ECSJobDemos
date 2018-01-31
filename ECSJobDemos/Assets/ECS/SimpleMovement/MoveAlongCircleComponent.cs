using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct MoveAlongCircle : IComponentData
    {
        public float3 center;
        public float radius;
        public float t;
    }

    public class MoveAlongCircleComponent : ComponentDataWrapper<MoveAlongCircle> { } 
}
