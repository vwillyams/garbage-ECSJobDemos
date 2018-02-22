﻿using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationSpeed : IComponentData
    {
        public float speed;
    }

    public class RotationSpeedComponent : ComponentDataWrapper<RotationSpeed> { } 
}
