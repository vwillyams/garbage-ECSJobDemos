using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Boids
{
	public struct BoidTarget : IComponentData { }

	public class BoidTargetComponent : ComponentDataWrapper<BoidTarget> { }
}