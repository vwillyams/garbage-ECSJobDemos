using System;
using Unity.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationSpeedResetSphere : IComponentData
    {
        public float speed;
    }

    public class RotationSpeedResetSphereComponent : ComponentDataWrapper<RotationSpeedResetSphere> { } 
}
