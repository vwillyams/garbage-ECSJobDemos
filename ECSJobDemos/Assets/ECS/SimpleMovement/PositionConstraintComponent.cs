using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;

namespace UnityEngine.ECS.SimpleMovement
{
    public struct PositionConstraint : IComponentData
    {
        public Entity parentEntity;
        public float distance;
    }

    public class PositionConstraintComponent : ComponentDataWrapper<PositionConstraint> { } 
}
