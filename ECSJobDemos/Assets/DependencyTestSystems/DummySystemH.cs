using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

[UpdateAfter(typeof(DummySystemG))]
public class DummySystemH : ComponentSystem {
	public override void OnUpdate()
	{
		
	}
}
