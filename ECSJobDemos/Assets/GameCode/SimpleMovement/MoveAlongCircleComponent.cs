using System;
using Unity.Entities;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct MoveAlongCircle : IComponentData
    {
        public float3 center;
        public float radius;
        [NonSerialized]
        public float t;
    }

    public class MoveAlongCircleComponent : ComponentDataWrapper<MoveAlongCircle> { } 
}

