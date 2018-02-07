using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.TransformShim
{
    public struct CopyInitialTransformFromGameObject : IComponentData { }

    public class CopyInitialTransformFromGameObjectComponent : ComponentDataWrapper<CopyInitialTransformFromGameObject> { } 
}
