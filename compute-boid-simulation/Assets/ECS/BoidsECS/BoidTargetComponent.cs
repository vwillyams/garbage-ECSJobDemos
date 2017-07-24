using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ECS
{
	public struct BoidTarget : IComponentData { }

	public class BoidTargetComponent : ComponentDataWrapper<BoidTarget> { }
}