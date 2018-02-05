using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleBounds
{
    [Serializable]
    public struct Radius : IComponentData
    {
        public float radius;
    }

    public class RadiusComponent : ComponentDataWrapper<Radius> { } 
}
