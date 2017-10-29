using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace BoidSimulations
{
	[System.Serializable]
	public struct BoidObstacle : IComponentData
	{
		// Cached value grabbed from transform component
		public float3 position;

		[SerializeField]
		public float  size;

		[SerializeField]
		public float aversionDistance;
	}

	public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
}