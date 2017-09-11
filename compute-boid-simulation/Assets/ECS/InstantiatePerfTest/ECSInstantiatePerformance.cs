using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.ECS;
using UnityEngine.Profiling;

// 64 + 16 + 12 + 128 = 220 bytes

struct Component64Bytes : IComponentData
{
    float4x4 value;
}

struct Component16Bytes : IComponentData
{
    float4 value;
}

struct Component12Bytes : IComponentData
{
    public float3 value;
}

struct Component128Bytes : IComponentData
{
    float4x4 value0;
    float4x4 value1;
}

public class ECSInstantiatePerformance : MonoBehaviour
{
	CustomSampler instantiateSampler;
	CustomSampler destroySampler;
	CustomSampler iterateSampler;

	void Awake()
	{
		instantiateSampler = CustomSampler.Create("InstantiateTest");
		destroySampler = CustomSampler.Create("DestroyTest");
		iterateSampler = CustomSampler.Create("IterateTest");
	}

	void Update()
	{
		UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Disabled;

		var oldRoot = DependencyManager.Root;
		DependencyManager.Root = new DependencyManager ();
		DependencyManager.SetDefaultCapacity (100000 * 2);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

        var archetype = m_EntityManager.AllocateEntity();
        m_EntityManager.AddComponent<Component128Bytes>(archetype, new Component128Bytes());
        m_EntityManager.AddComponent<Component12Bytes>(archetype, new Component12Bytes());
        m_EntityManager.AddComponent<Component64Bytes>(archetype, new Component64Bytes());
        m_EntityManager.AddComponent<Component16Bytes>(archetype, new Component16Bytes());

		var group0 = m_EntityManager.CreateEntityGroup(typeof(Component128Bytes));
        var group1 = m_EntityManager.CreateEntityGroup(typeof(Component12Bytes));
        var group2 = m_EntityManager.CreateEntityGroup(typeof(Component128Bytes));

		instantiateSampler.Begin ();
		var instances = m_EntityManager.Instantiate (archetype, 100000);
		instantiateSampler.End();

		iterateSampler.Begin ();
		var array = group1.GetComponentDataArray<Component12Bytes>();
		float sum = 0;
		for (int i = 0; i < array.Length; ++i)
			sum += array[i].value.x;
		iterateSampler.End ();

		destroySampler.Begin ();
		m_EntityManager.Destroy (instances);
		destroySampler.End ();

		instances.Dispose ();

        UnityEngine.Collections.NativeLeakDetection.Mode = UnityEngine.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}
