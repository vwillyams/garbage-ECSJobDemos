using UnityEngine.ECS;

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
}
