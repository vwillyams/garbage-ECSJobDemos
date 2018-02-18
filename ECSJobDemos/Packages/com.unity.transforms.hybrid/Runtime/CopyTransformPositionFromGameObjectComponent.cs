using Unity.ECS;

namespace Unity.Transforms.Hybrid
{
    public struct CopyTransformPositionFromGameObject : IComponentData { }

    public class CopyTransformPositionFromGameObjectComponent : ComponentDataWrapper<CopyTransformPositionFromGameObject> { } 
}
