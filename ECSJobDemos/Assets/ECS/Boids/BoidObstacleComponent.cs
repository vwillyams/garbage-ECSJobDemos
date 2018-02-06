using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Mathematics;

namespace UnityEngine.ECS.Boids
{
	public struct BoidObstacle : IComponentData { }

	public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
}