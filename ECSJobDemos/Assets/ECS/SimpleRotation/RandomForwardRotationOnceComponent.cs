using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    public struct RandomForwardRotationOnce : IComponentData { }

    public class RandomForwardRotationOnceComponent : ComponentDataWrapper<RandomForwardRotationOnce> { } 
}
