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

		m_EntityManager.CreateEntityGroup (typeof(Component128Bytes));
		m_EntityManager.CreateEntityGroup (typeof(Component12Bytes));
        m_EntityManager.CreateEntityGroup (typeof(Component128Bytes));

		instantiateSampler.Begin();

        var archetype = m_EntityManager.CreateEntity();
        m_EntityManager.AddComponent<Component128Bytes>(archetype, new Component128Bytes());
        m_EntityManager.AddComponent<Component12Bytes>(archetype, new Component12Bytes());
        m_EntityManager.AddComponent<Component64Bytes>(archetype, new Component64Bytes());

        var entities = new NativeArray<Entity>(100000, Allocator.Temp);
        m_EntityManager.Instantiate (archetype, entities);

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
            m_EntityManager.DestroyEntity(entities[i]);
        destroySampler.End();

        entities.Dispose ();


        UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}