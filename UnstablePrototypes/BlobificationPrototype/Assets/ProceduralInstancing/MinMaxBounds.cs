using Unity.Mathematics;

public struct MinMaxBounds
{
    public float3 Min;
    public float3 Max;

    public static MinMaxBounds Empty
    {
        get
        {
            MinMaxBounds bounds;
            bounds.Min = new float3(float.PositiveInfinity);
            bounds.Max = new float3(float.NegativeInfinity);
            return bounds;
        }
    }

    public bool IsEmpty
    {
        get
        {
            return math.all(new float3(float.PositiveInfinity) == Min) || math.all(new float3(float.NegativeInfinity) == Max);
        }
    }

    public void Encapsulate(float3 position)
    {
        Min = math.min(position, Min);
        Max = math.max(position, Max);
    }
}