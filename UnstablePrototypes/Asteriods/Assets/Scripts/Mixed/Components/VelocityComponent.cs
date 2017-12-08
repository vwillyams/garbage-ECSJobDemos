using UnityEngine;
using UnityEngine.ECS;

public struct VelocityComponentData : IComponentData
{
    public float dx;
    public float dy;

    public VelocityComponentData(float x, float y)
    {
        this.dx = x;
        this.dy = y;
    }
}

public class VelocityComponent : ComponentDataWrapper<VelocityComponentData> { }