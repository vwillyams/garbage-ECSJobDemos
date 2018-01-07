using Unity.Mathematics;
using UnityEngine.Assertions;

public struct Bounds
{
    public float3 Center;
    public float3 Extents;

    public static implicit operator Bounds(MinMaxBounds minMaxBounds)
    {
        Assert.IsFalse(minMaxBounds.IsEmpty);
        Bounds bounds;
        bounds.Center = (minMaxBounds.Min + minMaxBounds.Max) * 0.5F;
        bounds.Extents = (minMaxBounds.Max - minMaxBounds.Min) * 0.5F;
        return bounds;
    }
}