using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    public struct CopyInitialTransformRotationFromGameObject : IComponentData { }

    public class CopyInitialTransformRotationFromGameObjectComponent : ComponentDataWrapper<CopyInitialTransformRotationFromGameObject> { } 
}
