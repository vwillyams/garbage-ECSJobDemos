using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    public struct Gravity : IComponentData
    {
    }

    public class GravityComponent : ComponentDataWrapper<Gravity> { } 
}
