using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
	[UpdateAfter(typeof(DummySystemG))]
	public class DummySystemH : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}
}
