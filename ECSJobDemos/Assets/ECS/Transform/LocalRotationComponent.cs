using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;

namespace UnityEngine.ECS.Transform
{
    public struct LocalRotation : IComponentData
    {
        public quaternion value;
    }

    public class LocalRotationComponent : ComponentDataWrapper<LocalRotation> { } 
}
