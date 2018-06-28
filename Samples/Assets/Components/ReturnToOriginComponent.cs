using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct ReturnToOrigin : IComponentData
{
	public float3 origin;
    public float returnForce;
}

public class ReturnToOriginComponent : ComponentDataWrapper<ReturnToOrigin> { }