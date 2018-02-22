using Unity.Entities;

namespace Unity.Transforms.Hybrid
{
    public struct CopyTransformRotationFromGameObject : IComponentData { }

    public class CopyTransformRotationFromGameObjectComponent : ComponentDataWrapper<CopyTransformRotationFromGameObject> { } 
}
