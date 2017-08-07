using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace BoidSimulations
{
	public struct BoidGround : IComponentData { }

	public class BoidGroundComponent : ComponentDataWrapper<BoidGround> { }
}