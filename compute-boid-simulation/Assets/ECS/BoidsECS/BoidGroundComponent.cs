using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ECS
{
	public struct BoidGround : IComponentData { }

	public class BoidGroundComponent : ComponentDataWrapper<BoidGround> { }
}