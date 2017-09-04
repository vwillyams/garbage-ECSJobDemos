using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using UnityEngine.Collections;

public class ECSAddComponentPerformance : MonoBehaviour
{
	CustomSampler instantiateSampler;
	CustomSampler destroySampler;

	void Awake()
	{
		instantiateSampler = CustomSampler.Create("ECS.AddComponent");
		destroySampler = CustomSampler.Create("ECS.RemoveComponent");
	}

	void Update()
	{
		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Disabled;

		var oldRoot = DependencyManager.Root;
		DependencyManager.Root = new DependencyManager ();
		DependencyManager.SetDefaultCapacity (100000);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

		var group0 = new EntityGroup (m_EntityManager, typeof(BoidSimulations.BoidData));
		var group1 = new EntityGroup (m_EntityManager, typeof(BoidSimulations.BoidData));

		var entities = new NativeArray<Entity>(100000, Allocator.Temp);

		instantiateSampler.Begin();
		for (int i = 0;i<100000;i++)
		{
			entities[i] = m_EntityManager.AllocateEntity ();
			m_EntityManager.AddComponent (entities[i], new BoidSimulations.BoidData());
		}
		instantiateSampler.End();

		destroySampler.Begin ();
		for (int i = 0;i<100000;i++)
		{
			m_EntityManager.RemoveComponent<BoidSimulations.BoidData> (entities[i]);
		}
		destroySampler.End ();

		entities.Dispose ();

		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}
