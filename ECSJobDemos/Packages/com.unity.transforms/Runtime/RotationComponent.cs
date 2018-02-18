using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    [System.Serializable]
    public struct Rotation : IComponentData
    {
        public quaternion value;
    }

    public class RotationComponent : ComponentDataWrapper<Rotation> { }
}