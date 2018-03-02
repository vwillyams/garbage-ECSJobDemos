﻿using System;
using Unity.Entities;
using Unity.Mathematics;

namespace UnityEngine.ECS.SimpleMovement
{
    /// <summary>
    /// This component will update the corresponding TransformPositionComponent associated with this component at the
    /// rate specified by the MoveSpeedComponent, also associated with this component in radians per second.
    /// </summary>
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

