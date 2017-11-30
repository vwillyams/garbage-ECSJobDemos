using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

[UpdateAfter(typeof(DummySystemF))]
public class DummySystemG : ComponentSystem {
	public override void OnUpdate()
	{
		
	}
}
