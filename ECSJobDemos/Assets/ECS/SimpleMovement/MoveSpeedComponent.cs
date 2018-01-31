using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    [Serializable]
    public struct MoveSpeed : IComponentData
    {
        public float speed;
    }

    public class MoveSpeedComponent : ComponentDataWrapper<MoveSpeed> { } 
}
