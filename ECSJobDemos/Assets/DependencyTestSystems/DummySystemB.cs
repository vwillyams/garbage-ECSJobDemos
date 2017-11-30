using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
//[UpdateAfter(typeof(DummySystemA))]
	public class DummySystemB : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}
}
