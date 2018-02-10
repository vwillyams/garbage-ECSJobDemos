namespace UnityEngine.ECS.Transform
{
    public struct TransformParent : IComponentData
    {
        public Entity parent;
    }

    public class TransformParentComponent : ComponentDataWrapper<TransformParent> { }
}