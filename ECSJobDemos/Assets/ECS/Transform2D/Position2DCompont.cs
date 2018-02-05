using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Transform2D
{
    [Serializable]
    public struct Position2D : IComponentData
    {
        public float2 position;
    }

    public class Position2DComponent : ComponentDataWrapper<Position2D> { } 
}
