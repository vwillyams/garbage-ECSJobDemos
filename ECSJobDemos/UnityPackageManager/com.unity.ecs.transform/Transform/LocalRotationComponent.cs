using Unity.Mathematics;

namespace UnityEngine.ECS.Transform
{
    public struct LocalRotation : IComponentData
    {
        public quaternion value;
    }

    public class LocalRotationComponent : ComponentDataWrapper<LocalRotation> { } 
}
