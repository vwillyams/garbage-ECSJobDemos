using UnityEngine;
using Unity.ECS;

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
