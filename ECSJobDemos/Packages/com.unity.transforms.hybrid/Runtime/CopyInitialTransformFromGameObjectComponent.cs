using Unity.ECS;

namespace Unity.Transforms.Hybrid
{
    public struct CopyInitialTransformFromGameObject : IComponentData { }

    public class CopyInitialTransformFromGameObjectComponent : ComponentDataWrapper<CopyInitialTransformFromGameObject> { } 
}
