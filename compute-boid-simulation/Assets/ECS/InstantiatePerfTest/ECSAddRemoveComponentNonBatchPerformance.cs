using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using UnityEngine.Collections;

public class ECSAddRemoveComponentNonBatchPerformance : MonoBehaviour
{
    CustomSampler addSampler;
    CustomSampler instantiateSampler;
    CustomSampler destroySampler;
    CustomSampler removeSampler;

    void Awake()
	{
        instantiateSampler = CustomSampler.Create("ECS.Create");
        addSampler = CustomSampler.Create("ECS.AddComponent");
        destroySampler = CustomSampler.Create("ECS.Destroy");
        removeSampler = CustomSampler.Create("ECS.RemoveComponent");
    }

	void Update()
	{
		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Disabled;

		var oldRoot = DependencyManager.Root;
		DependencyManager.Root = new DependencyManager ();
		DependencyManager.SetDefaultCapacity (100000);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

		var group0 = new EntityGroup (m_EntityManager, typeof(Component128Bytes));
		var group1 = new EntityGroup (m_EntityManager, typeof(Component12Bytes));
        var group2 = new EntityGroup(m_EntityManager, typeof(Component128Bytes));

		instantiateSampler.Begin();

        var archetype = m_EntityManager.AllocateEntity();
        m_EntityManager.AddComponent<Component128Bytes>(archetype, new Component128Bytes());
        m_EntityManager.AddComponent<Component12Bytes>(archetype, new Component12Bytes());
        m_EntityManager.AddComponent<Component64Bytes>(archetype, new Component64Bytes());

        var entities = m_EntityManager.Instantiate (archetype, 100000);

		instantiateSampler.End();

        addSampler.Begin();
        for (int i = 0; i < entities.Length; i++)
            m_EntityManager.AddComponent(entities[i], new Component16Bytes());
        addSampler.End();

        removeSampler.Begin();
		for (int i = 0;i<entities.Length;i++)
			m_EntityManager.RemoveComponent<Component16Bytes> (entities[i]);
        removeSampler.End();

        destroySampler.Begin();
        for (int i = 0; i < entities.Length; i++)
            m_EntityManager.Destroy(entities[i]);
        destroySampler.End();

        entities.Dispose ();

        group0.Dispose();
        group1.Dispose();
        group2.Dispose();


        UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}