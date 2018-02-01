using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    public struct MoveForward : IComponentData
    {
    }

    public class MoveForwardComponent : ComponentDataWrapper<MoveForward> { } 
}
