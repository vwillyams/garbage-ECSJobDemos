using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.TransformShim
{
    public struct CopyTransformRotationFromGameObject : IComponentData { }

    public class CopyTransformRotationFromGameObjectComponent : ComponentDataWrapper<CopyTransformRotationFromGameObject> { } 
}
