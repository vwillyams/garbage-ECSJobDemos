﻿using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Profiling;


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
        World oldRoot = null;
        if (PerformanceTestConfiguration.CleanManagers)
        {
            oldRoot = World.Active;
            World.Active = new World("AddRemoveComponentNonBatchPerformance");
            World.Active.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount);
        }

	    var entityManager = World.Active.GetOrCreateManager<EntityManager>();
	    var dummySystem = World.Active.GetOrCreateManager<DummyComponentSystem>();

	    dummySystem.GetComponentGroup(typeof(Component128Bytes));
	    dummySystem.GetComponentGroup(typeof(Component12Bytes));
	    dummySystem.GetComponentGroup(typeof(Component128Bytes));

        var archetype = entityManager.CreateArchetype(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes));
        var entities = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount, Allocator.Temp);

	    for (int k = 0; k != PerformanceTestConfiguration.Iterations; k++)
	    {
	        instantiateSampler.Begin();
	        for (int i = 0; i < entities.Length; i++)
	            entities[i] = entityManager.CreateEntity (archetype);
	        instantiateSampler.End();

	        addSampler.Begin();
	        for (int i = 0; i < entities.Length; i++)
	            entityManager.AddComponentData(entities[i], new Component16Bytes());
	        addSampler.End();

	        removeSampler.Begin();
	        for (int i = 0;i<entities.Length;i++)
	            entityManager.RemoveComponent<Component16Bytes> (entities[i]);
	        removeSampler.End();

	        destroySampler.Begin();
	        for (int i = 0; i < entities.Length; i++)
	            entityManager.DestroyEntity(entities[i]);
	        destroySampler.End();
	    }

        entities.Dispose ();

        if (oldRoot != null)
        {
            World.Active.Dispose();
            World.Active = oldRoot;
        }
	}
}
