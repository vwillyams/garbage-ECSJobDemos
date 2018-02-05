using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleRotation
{
    public struct RandomInitialHeading : IComponentData { }

    public class RandomInitialHeadingComponent : ComponentDataWrapper<RandomInitialHeading> { } 
}
