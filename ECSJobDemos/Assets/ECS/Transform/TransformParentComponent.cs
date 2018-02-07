using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Transform
{
    public struct TransformParent : IComponentData
    {
        public Entity parent;
    }

    public class TransformParentComponent : ComponentDataWrapper<TransformParent> { }
}