using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
	[UpdateAfter(typeof(DummySystemE))]
	[UpdateAfter(typeof(DummySystemB))]
	public class DummySystemF : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}
}
