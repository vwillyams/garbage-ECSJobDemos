using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct RotationSpeed : IComponentData
{
	public float Value;

	public RotationSpeed(float speed)
	{
		Value = speed;
	}
}

public class RotationSpeedComponent : ComponentDataWrapper<RotationSpeed> { }