using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform
{
    [Serializable]
    public struct Position : IComponentData
    {
        public float3 position;
    }

    public class PositionComponent : ComponentDataWrapper<Position> { } 
}
