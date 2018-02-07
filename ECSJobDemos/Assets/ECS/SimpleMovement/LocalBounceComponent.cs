﻿using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct LocalBounce : IComponentData
    {
        [NonSerialized] public float t;
        public float speed;
        public float3 height;
    }

    public class LocalBounceComponent : ComponentDataWrapper<LocalBounce> { } 
}
