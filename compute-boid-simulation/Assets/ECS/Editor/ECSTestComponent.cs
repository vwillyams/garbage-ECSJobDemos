using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;

namespace ECS
{
	public struct EcsTestData : IComponentData
	{
		public int value;

		public EcsTestData(int inValue) { value = inValue; }
	}

	[ExecuteInEditMode]
	public class EcsTestComponent : ComponentDataWrapper<EcsTestData> { }
}