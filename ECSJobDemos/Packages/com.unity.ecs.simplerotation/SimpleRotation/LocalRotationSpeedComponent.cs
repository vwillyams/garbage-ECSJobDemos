using System;
using Unity.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct LocalRotationSpeed : IComponentData
    {
        public float speed;
    }

    public class LocalRotationSpeedComponent : ComponentDataWrapper<LocalRotationSpeed> { } 
}
