using Unity.Entities;

namespace UnityEngine.ECS.SimpleMovement
{
    public struct PositionConstraint : IComponentData
    {
        public Entity parentEntity;
        public float maxDistance;
    }

    public class PositionConstraintComponent : ComponentDataWrapper<PositionConstraint> { } 
}
