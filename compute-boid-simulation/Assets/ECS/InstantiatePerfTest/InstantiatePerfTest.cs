using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;

public class InstantiatePerfTest : ScriptBehaviour
{
	public GameObject prefab;

	[InjectDependency]
	EntityManager m_EntityManager;

	protected override void OnUpdate()
	{
		base.OnUpdate ();

//		Profiler.BeginSample ("Instantiate");
		var instances = m_EntityManager.Instantiate (prefab, 100000);
//		Profiler.EndSample ();

//		Profiler.BeginSample ("Destroy");
		m_EntityManager.Destroy (instances);
//		Profiler.EndSample ();

		instances.Dispose ();
	}
}
