using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Tests
{
	public struct EcsTestData : IComponentData
	{
		public int value;

		public EcsTestData(int inValue) { value = inValue; }
	}

	[ExecuteInEditMode]
	public class EcsTestComponent : ComponentDataWrapper<EcsTestData> { }
}