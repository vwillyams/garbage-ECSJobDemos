using Unity.Entities;

namespace Unity.Transforms.Hybrid
{
    public struct CopyTransformPositionFromGameObject : IComponentData { }

    public class CopyTransformPositionFromGameObjectComponent : ComponentDataWrapper<CopyTransformPositionFromGameObject> { } 
}
