using Unity.Entities;
using Unity.Mathematics;

public struct PositionComponentData : IComponentData
{
    public float x;
    public float y;

    public PositionComponentData(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public struct RotationComponentData : IComponentData
{
    public float angle;

    public RotationComponentData(float angle)
    {
        this.angle = angle;
    }

    public static float2 rotate(float2 pos, float angle)
    {
        angle = math.radians(angle);
        float sinA = math.sin(angle);
        float cosA = math.cos(angle);
        return new float2(math.dot(pos, new float2(cosA, -sinA)), math.dot(pos, new float2(sinA, cosA)));
    }
}
