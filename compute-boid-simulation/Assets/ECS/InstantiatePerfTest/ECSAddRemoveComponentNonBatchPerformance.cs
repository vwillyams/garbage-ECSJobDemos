using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using UnityEngine.Collections;

public class ECSAddRemoveComponentNonBatchPerformance : MonoBehaviour
{
    const int kInstanceCount = 100 * 1000;

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
		DependencyManager.SetDefaultCapacity (kInstanceCount);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

		m_EntityManager.CreateComponentGroup (typeof(Component128Bytes));
		m_EntityManager.CreateComponentGroup (typeof(Component12Bytes));
        m_EntityManager.CreateComponentGroup (typeof(Component128Bytes));

        var archetype = m_EntityManager.CreateArchetype(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes));
        var entities = new NativeArray<Entity>(kInstanceCount, Allocator.Temp);

        instantiateSampler.Begin();
        for (int i = 0; i < entities.Length; i++)
            entities[i] = m_EntityManager.CreateEntity (archetype);

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