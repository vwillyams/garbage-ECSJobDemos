using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Boids
{
	[Serializable]
	public struct BoidObstacle : IComponentData
	{
		// Cached value grabbed from transform component
		public float3 position;

		public float size;
		public float aversionDistance;
	}

	public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
}