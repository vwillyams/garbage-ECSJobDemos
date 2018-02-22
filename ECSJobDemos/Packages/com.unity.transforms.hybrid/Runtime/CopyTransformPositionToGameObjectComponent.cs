using Unity.Entities;

namespace Unity.Transforms.Hybrid
{
    public struct CopyTransformPositionToGameObject : IComponentData { }

    public class CopyTransformPositionToGameObjectComponent : ComponentDataWrapper<CopyTransformPositionToGameObject> { } 
}
