using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationSpeedSlowdown : IComponentData
    {
        public float speed;
    }

    public class RotationSpeedSlowdownComponent : ComponentDataWrapper<RotationSpeedSlowdown> { } 
}
