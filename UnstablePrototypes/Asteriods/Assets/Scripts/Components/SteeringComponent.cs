using UnityEngine;
using UnityEngine.ECS;

public struct SteeringComponentData : IComponentData
{
    public float angle;
    public float dx;
    public float dy;

    public SteeringComponentData(float angle, float x, float y)
    {
        this.angle = angle;
        this.dx = x;
        this.dy = y;
    }
}

public class SteeringComponent : ComponentDataWrapper<SteeringComponentData> { }