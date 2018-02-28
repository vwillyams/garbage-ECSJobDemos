using System;
using Unity.Entities;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct LocalRotationSpeed : IComponentData
    {
        public float Value;
    }

    public class LocalRotationSpeedComponent : ComponentDataWrapper<LocalRotationSpeed> { } 
}
