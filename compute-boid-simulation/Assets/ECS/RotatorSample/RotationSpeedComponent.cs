using System;
using UnityEngine;

namespace ECS
{
	public class RotationSpeedComponent : ScriptBehaviour
	{
		public float speed;
	}

	public class RotatingSystem : ComponentSystem
	{
		// NOTE: InjectTuples scans all [InjectTuples] in the class
		// and returns the union of objects that have both Transform and LightRotator
		[InjectTuples]
		public ComponentArray<Transform> 				m_Transforms;

		[InjectTuples]
		public ComponentArray<RotationSpeedComponent>   m_Rotators;

		override protected void OnUpdate()
		{
			base.OnUpdate ();

			float dt = Time.deltaTime;
			for (int i = 0; i != m_Transforms.Length;i++)
			{
				m_Transforms[i].rotation = m_Transforms[i].rotation * Quaternion.AngleAxis(dt * m_Rotators[i].speed, Vector3.up);
			}
		}
	}
}

