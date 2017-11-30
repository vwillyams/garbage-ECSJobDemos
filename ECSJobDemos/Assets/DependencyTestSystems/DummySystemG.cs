using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
	[UpdateAfter(typeof(DummySystemF))]
	public class DummySystemG : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}
}
