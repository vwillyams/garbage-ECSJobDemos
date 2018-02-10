using Unity.Mathematics;

namespace UnityEngine.ECS.Transform
{
    public struct Rotation : IComponentData
    {
        public quaternion value;
    }
    
    public class RotationComponent : ComponentDataWrapper<Rotation> { } 
}
