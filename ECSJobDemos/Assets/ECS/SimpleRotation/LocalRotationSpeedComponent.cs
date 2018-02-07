using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    [Serializable]
    public struct LocalRotationSpeed : IComponentData
    {
        public float speed;
    }

    public class LocalRotationSpeedComponent : ComponentDataWrapper<LocalRotationSpeed> { } 
}
