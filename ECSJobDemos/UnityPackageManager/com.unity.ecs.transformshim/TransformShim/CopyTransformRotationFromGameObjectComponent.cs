using Unity.ECS;

namespace UnityEngine.ECS.TransformShim
{
    public struct CopyTransformRotationFromGameObject : IComponentData { }

    public class CopyTransformRotationFromGameObjectComponent : ComponentDataWrapper<CopyTransformRotationFromGameObject> { } 
}
