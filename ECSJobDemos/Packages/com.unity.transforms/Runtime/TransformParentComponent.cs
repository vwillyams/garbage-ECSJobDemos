using Unity.ECS;

namespace Unity.Transforms
{
    public struct TransformParent : IComponentData
    {
        public Entity Value;
    }

    public class TransformParentComponent : ComponentDataWrapper<TransformParent> { }
}