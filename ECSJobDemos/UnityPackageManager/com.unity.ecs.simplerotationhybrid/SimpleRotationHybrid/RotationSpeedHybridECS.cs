using Unity.ECS;
using UnityEngine;
using UnityEngine.ECS;

namespace RotatorSamples
{
	public class RotationSpeedHybridECS : MonoBehaviour
	{
		public float speed;
	}

	public class RotatingSystemHybridECS : ComponentSystem
	{
		struct Group
		{
			public Transform 				transform;
			public RotationSpeedHybridECS   rotation;
		}

		protected override void OnUpdate()
		{
			float dt = Time.deltaTime;
			foreach(var e in GetEntities<Group>())
			{
				e.transform.rotation = e.transform.rotation * Quaternion.AngleAxis(dt * e.rotation.speed, Vector3.up);
			}
		}
	}
}

