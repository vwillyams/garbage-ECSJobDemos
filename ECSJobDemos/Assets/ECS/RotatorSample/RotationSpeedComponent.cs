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
			public Transform 				transform;
			public RotationSpeedComponent   rotation;
		}

		public override void OnUpdate()
		{
			float dt = Time.deltaTime;
			foreach(var e in GetEntities<Group>())
			{
				e.transform.rotation = e.transform.rotation * Quaternion.AngleAxis(dt * e.rotation.speed, Vector3.up);
			}
		}
	}
}

