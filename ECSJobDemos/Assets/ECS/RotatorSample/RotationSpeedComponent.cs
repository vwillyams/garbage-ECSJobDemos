using System;
using UnityEngine;
using UnityEngine.ECS;

namespace RotatorSamples
{
	public class RotationSpeedComponent : MonoBehaviour
	{
		public float speed;
	}

	public class RotatingSystem : ComponentSystem
	{
		struct Group
		{
			public ComponentArray<Transform> 				transforms;
			public ComponentArray<RotationSpeedComponent>   rotators;
		}

		[InjectComponentGroup]
		Group m_Rotators;

		override public void OnUpdate()
		{
			base.OnUpdate ();

			float dt = Time.deltaTime;
			for (int i = 0; i != m_Rotators.transforms.Length;i++)
			{
				m_Rotators.transforms[i].rotation = m_Rotators.transforms[i].rotation * Quaternion.AngleAxis(dt * m_Rotators.rotators[i].speed, Vector3.up);
			}
		}
	}
}

