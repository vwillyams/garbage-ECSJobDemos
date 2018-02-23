using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationSpeedResetSphere : IComponentData
    {
        public float speed;
    }

    public class RotationSpeedResetSphereComponent : ComponentDataWrapper<RotationSpeedResetSphere> { } 
}
