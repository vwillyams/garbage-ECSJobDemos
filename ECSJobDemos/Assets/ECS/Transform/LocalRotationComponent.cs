using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    public struct LocalRotation : IComponentData
    {
        public quaternion rotation;
    }

    public class LocalRotationComponent : ComponentDataWrapper<LocalRotation> { } 
}
