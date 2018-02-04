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
		public float aversionDistance;
	}

	public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
}