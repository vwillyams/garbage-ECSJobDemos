using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationAcceleration : IComponentData
    {
        public float speed;
    }

    public class RotationAccelerationComponent : ComponentDataWrapper<RotationAcceleration> { } 
}
