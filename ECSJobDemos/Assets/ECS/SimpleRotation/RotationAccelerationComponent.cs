using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationAcceleration : IComponentData
    {
        public float speed;
    }

    public class RotationAccelerationComponent : ComponentDataWrapper<RotationAcceleration> { } 
}
