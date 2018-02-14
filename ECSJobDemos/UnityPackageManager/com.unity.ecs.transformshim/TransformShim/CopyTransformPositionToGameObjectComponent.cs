using Unity.ECS;

namespace UnityEngine.ECS.TransformShim
{
    public struct CopyTransformPositionToGameObject : IComponentData { }

    public class CopyTransformPositionToGameObjectComponent : ComponentDataWrapper<CopyTransformPositionToGameObject> { } 
}
