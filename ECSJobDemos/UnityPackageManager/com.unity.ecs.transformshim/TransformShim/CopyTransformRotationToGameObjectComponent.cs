using Unity.ECS;

namespace UnityEngine.ECS.TransformShim
{
    public struct CopyTransformRotationToGameObject : IComponentData { }

    public class CopyTransformRotationToGameObjectComponent : ComponentDataWrapper<CopyTransformRotationToGameObject> { } 
}
