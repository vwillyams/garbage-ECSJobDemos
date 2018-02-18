using Unity.ECS;

namespace Unity.Transforms.Hybrid
{
    public struct CopyTransformRotationToGameObject : IComponentData { }

    public class CopyTransformRotationToGameObjectComponent : ComponentDataWrapper<CopyTransformRotationToGameObject> { } 
}
