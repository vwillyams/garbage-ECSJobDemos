using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS.Tests
{
	[UpdateBefore(typeof(DummySystemB))]
	public class DummySystemA : ComponentSystem {
		public override void OnUpdate()
		{
		
		}
	}
}
