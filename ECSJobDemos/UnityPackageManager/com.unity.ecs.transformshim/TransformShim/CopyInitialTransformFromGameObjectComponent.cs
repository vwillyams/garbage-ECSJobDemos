using Unity.ECS;

namespace UnityEngine.ECS.TransformShim
{
    public struct CopyInitialTransformFromGameObject : IComponentData { }

    public class CopyInitialTransformFromGameObjectComponent : ComponentDataWrapper<CopyInitialTransformFromGameObject> { } 
}
