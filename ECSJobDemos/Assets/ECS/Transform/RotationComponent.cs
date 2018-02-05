using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    public struct Rotation : IComponentData
    {
        public quaternion rotation;
    }

    public class RotationComponent : ComponentDataWrapper<Rotation> { } 
}
