using System;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.ECS;

namespace BoidSimulations
{
	public struct BoidInstanceRenderer : IComponentData
	{
	}

	public class BoidInstanceRendererComponent : ComponentDataWrapper<BoidInstanceRenderer> { }
}