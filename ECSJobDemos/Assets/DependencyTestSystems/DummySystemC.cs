using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

[UpdateAfter(typeof(DummySystemB))]
public class DummySystemC : ComponentSystem {
	public override void OnUpdate()
	{
		
	}
}
