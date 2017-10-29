using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

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
    const int kInstanceCount = 100 * 1000;

    CustomSampler instantiateSampler;
	CustomSampler destroySampler;
	CustomSampler iterateSampler;
    CustomSampler memcpySampler;
    CustomSampler memcpy12Sampler;

    void Awake()
	{
		instantiateSampler = CustomSampler.Create("InstantiateTest");
		destroySampler = CustomSampler.Create("DestroyTest");
        iterateSampler = CustomSampler.Create("IterateTest");
        memcpySampler = CustomSampler.Create("Memcpy - All component size");
        memcpy12Sampler = CustomSampler.Create("Memcpy - Component 12 bytes");
    }

    unsafe void Update()
	{
		Unity.Collections.NativeLeakDetection.Mode = Unity.Collections.NativeLeakDetectionMode.Disabled;

		var oldRoot = DependencyManager.Root;
		DependencyManager.Root = new DependencyManager ();
		DependencyManager.SetDefaultCapacity (kInstanceCount * 2);

		var m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();

        int size = sizeof(Component128Bytes) + sizeof(Component12Bytes) + sizeof(Component64Bytes) + sizeof(Component64Bytes) + sizeof(Component16Bytes) + sizeof(Entity);
        size *= kInstanceCount;
        var src = UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
        var dst = UnsafeUtility.Malloc(size, 64, Allocator.Persistent);

        memcpySampler.Begin();
        UnsafeUtility.MemCpy(dst, src, size);
        memcpySampler.End();


        memcpy12Sampler.Begin();
        UnsafeUtility.MemCpy(dst, src, sizeof(Component12Bytes) * kInstanceCount);
        memcpy12Sampler.End();

        UnsafeUtility.Free(src, Allocator.Persistent);
        UnsafeUtility.Free(dst, Allocator.Persistent);



        var archetype = m_EntityManager.CreateEntity();
        m_EntityManager.AddComponent<Component128Bytes>(archetype, new Component128Bytes());
        m_EntityManager.AddComponent<Component12Bytes>(archetype, new Component12Bytes());
        m_EntityManager.AddComponent<Component64Bytes>(archetype, new Component64Bytes());
        m_EntityManager.AddComponent<Component16Bytes>(archetype, new Component16Bytes());

		m_EntityManager.CreateComponentGroup(typeof(Component128Bytes));
        var group1 = m_EntityManager.CreateComponentGroup(typeof(Component12Bytes));
        m_EntityManager.CreateComponentGroup(typeof(Component128Bytes));

		instantiateSampler.Begin ();
		var instances = new NativeArray<Entity>(kInstanceCount, Allocator.Temp);
		m_EntityManager.Instantiate (archetype, instances);
		instantiateSampler.End();

		iterateSampler.Begin ();
		var array = group1.GetComponentDataArray<Component12Bytes>();
		float sum = 0;
		for (int i = 0; i < array.Length; ++i)
			sum += array[i].value.x;
		iterateSampler.End ();

		destroySampler.Begin ();
		m_EntityManager.DestroyEntity (instances);
		destroySampler.End ();

		instances.Dispose ();

        Unity.Collections.NativeLeakDetection.Mode = Unity.Collections.NativeLeakDetectionMode.Enabled;

		DependencyManager.Root.Dispose ();
		DependencyManager.Root = oldRoot;
	}
}
