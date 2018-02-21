using System;
using Unity.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct LocalRotationSpeed : IComponentData
    {
        public float Value { get; set; }
    }

    public class LocalRotationSpeedComponent : ComponentDataWrapper<LocalRotationSpeed> { } 
}
