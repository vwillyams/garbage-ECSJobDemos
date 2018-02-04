using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    public struct CopyInitialTransformPositionFromGameObject : IComponentData { }

    public class CopyInitialTransformPositionFromGameObjectComponent : ComponentDataWrapper<CopyInitialTransformPositionFromGameObject> { } 
}
