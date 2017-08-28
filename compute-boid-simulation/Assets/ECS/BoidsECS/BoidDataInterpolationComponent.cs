using System;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.ECS;

namespace BoidSimulations
{
	public struct BoidDataInterpolation : IComponentData
	{
		public BoidData previousFrame;
		public BoidData thisFrame;
	}


	public class BoidDataInterpolationComponent : ComponentDataWrapper<BoidDataInterpolation>
	{
	}
}