﻿using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleBounds
{
    [Serializable]
    public struct Radius : IComponentData
    {
        public float radius;
    }

    public class RadiusComponent : ComponentDataWrapper<Radius> { } 
}