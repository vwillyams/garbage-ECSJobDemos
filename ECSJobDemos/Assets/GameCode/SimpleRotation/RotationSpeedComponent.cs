using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct RotationSpeed : IComponentData
    {
        public float Value;
    }

    public class RotationSpeedComponent : ComponentDataWrapper<RotationSpeed> { } 
}
