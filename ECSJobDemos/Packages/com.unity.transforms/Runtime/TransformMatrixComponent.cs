using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    public struct TransformMatrix : IComponentData
    {
        public float4x4 Value;
    }

    public class TransformMatrixComponent : ComponentDataWrapper<TransformMatrix> { }
}