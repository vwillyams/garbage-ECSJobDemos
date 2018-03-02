using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// Store a (calculated) float4x4 matrix representing the Object to World position/rotation/scale transformation.
    /// Required by other systems. e.g. MeshInstanceRenderer
    /// </summary>
    public struct TransformMatrix : IComponentData
    {
        public float4x4 Value { get; set; }
    }

    public class TransformMatrixComponent : ComponentDataWrapper<TransformMatrix> { }
}