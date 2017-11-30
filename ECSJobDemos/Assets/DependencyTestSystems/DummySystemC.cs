using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
	[UpdateAfter(typeof(DummySystemB))]
	public class DummySystemC : ComponentSystem
	{
		public override void OnUpdate()
		{

		}
	}
}
