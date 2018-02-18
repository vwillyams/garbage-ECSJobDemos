using Unity.ECS;

namespace Unity.Transforms
{
    public struct TransformParent : IComponentData
    {
        public Entity parent;
    }

    public class TransformParentComponent : ComponentDataWrapper<TransformParent> { }
}