using Unity.ECS;

namespace Unity.Transforms.Hybrid
{
    public struct CopyTransformPositionToGameObject : IComponentData { }

    public class CopyTransformPositionToGameObjectComponent : ComponentDataWrapper<CopyTransformPositionToGameObject> { } 
}
