using System;
using UnityEngine;
using UnityEngine.Collections;

namespace ECS
{
	public struct BoidInstanceRenderer : IComponentData
	{
	}

	public class BoidInstanceRendererComponent : ComponentDataWrapper<BoidInstanceRenderer> { }
}