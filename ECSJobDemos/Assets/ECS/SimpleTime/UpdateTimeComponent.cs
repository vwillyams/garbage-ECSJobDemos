using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleTime
{
    public struct UpdateTime : IComponentData
    {
        public float t;
    }

    public class UpdateTimeComponent : ComponentDataWrapper<UpdateTime> { } 
}
