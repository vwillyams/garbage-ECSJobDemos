using Unity.ECS;
using Unity.Mathematics;

namespace Unity.Transforms
{
    public struct TransformMatrix : IComponentData
    {
        public float4x4 matrix;
    }

    public class TransformMatrixComponent : ComponentDataWrapper<TransformMatrix> { }
}