using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;

public class ECSInstantiatePerformance : MonoBehaviour
{
	CustomSampler instantiateSampler;
	CustomSampler destroySampler;

	void Awake()
	{
		instantiateSampler = CustomSampler.Create("InstantiateTest");
		destroySampler = CustomSampler.Create("DestroyTest");
	}

	void Update()
	{
		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Disabled;

		var oldRoot = DependencyManager.Root;
		DependencyManager.Root = new DependencyManager ();
		DependencyManager.SetDefaultCapacity (100000 * 2);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

		var group0 = new EntityGroup (m_EntityManager, typeof(BoidSimulations.BoidData));
		var group1 = new EntityGroup (m_EntityManager, typeof(BoidSimulations.BoidData));

		var entity = m_EntityManager.AllocateEntity ();
		m_EntityManager.AddComponent (entity, new BoidSimulations.BoidData());

		instantiateSampler.Begin ();
		var instances = m_EntityManager.Instantiate (entity, 100000);
		instantiateSampler.End();

		destroySampler.Begin ();
		m_EntityManager.Destroy (instances);
		destroySampler.End ();

		instances.Dispose ();

		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}
