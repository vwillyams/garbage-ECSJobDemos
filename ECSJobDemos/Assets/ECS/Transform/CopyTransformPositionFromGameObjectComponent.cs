using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    public struct CopyTransformPositionFromGameObject : IComponentData { }

    public class CopyTransformPositionFromGameObjectComponent : ComponentDataWrapper<CopyTransformPositionFromGameObject> { } 
}
