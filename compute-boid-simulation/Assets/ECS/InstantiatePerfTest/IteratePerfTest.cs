using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;

public struct IteratePerfData : IComponentData
{
	public int random;
}
public class IteratePerfTest : MonoBehaviour
{
	CustomSampler iterateSampler;
	CustomSampler getSampler;

	void Awake()
	{
		iterateSampler = CustomSampler.Create("IterateTest");
		getSampler = CustomSampler.Create("GetComponentDataTest");
	}

	void Update()
	{
		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Disabled;

		var oldRoot = DependencyManager.Root;
		DependencyManager.Root = new DependencyManager ();
		DependencyManager.SetDefaultCapacity (100000 * 3);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

		var group0 = new EntityGroup (m_EntityManager, typeof(IteratePerfData));
		var group1 = new EntityGroup (m_EntityManager, typeof(BoidSimulations.BoidData));

		var entity = m_EntityManager.AllocateEntity ();
		m_EntityManager.AddComponent (entity, new BoidSimulations.BoidData());
		m_EntityManager.AddComponent (entity, new IteratePerfData());

		var instances = m_EntityManager.Instantiate (entity, 100000);

		var entity2 = m_EntityManager.AllocateEntity ();
		m_EntityManager.AddComponent (entity2, new IteratePerfData());

		var instances2 = m_EntityManager.Instantiate (entity2, 100000);

		int sum = 0;
		getSampler.Begin ();
		var components = group0.GetComponentDataArray<IteratePerfData>();
		getSampler.End ();

		iterateSampler.Begin ();
		for (int i = 0; i < components.Length; ++i)
			sum += components[i].random;
		iterateSampler.End ();

		m_EntityManager.Destroy (instances);

		instances.Dispose ();
		instances2.Dispose ();

		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}
