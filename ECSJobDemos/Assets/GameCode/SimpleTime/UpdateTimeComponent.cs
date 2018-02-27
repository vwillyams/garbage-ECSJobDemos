﻿using Unity.Entities;

namespace UnityEngine.ECS.SimpleTime
{
    public struct UpdateTime : IComponentData
    {
        public float t;
    }

    public class UpdateTimeComponent : ComponentDataWrapper<UpdateTime> { } 
}