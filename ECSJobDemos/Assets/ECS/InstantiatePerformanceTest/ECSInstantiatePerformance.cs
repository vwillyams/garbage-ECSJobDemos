using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;

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

struct Component4Bytes : IComponentData
{
	public float value;
}

struct Component128Bytes : IComponentData
{
    float4x4 value0;
    float4x4 value1;
}

public class ECSInstantiatePerformance : MonoBehaviour
{
	CustomSampler setupSampler;
	CustomSampler instantiateSampler;
	CustomSampler destroySampler;
	CustomSampler iterateSampler;
    CustomSampler memcpySampler;
	CustomSampler instantiateMemcpySampler ;
	CustomSampler iterateUnsafeSampler;
	CustomSampler iterateArraySampler;
	CustomSampler iterateNativeArraySampler;
	CustomSampler iterateNativeSliceSampler;
	 
    void Awake()
	{
		setupSampler = CustomSampler.Create("Setup");
		instantiateSampler = CustomSampler.Create("InstantiateTest");
		instantiateMemcpySampler = CustomSampler.Create("InstantiateTest - Memcpy");
		destroySampler = CustomSampler.Create("DestroyTest");
        iterateSampler = CustomSampler.Create("IterateTest - ComponentDataArray<Component4Bytes>");
        memcpySampler = CustomSampler.Create("Iterate - Memcpy");
		iterateUnsafeSampler = CustomSampler.Create("Iterate - Unsafe Ptr float");
		iterateArraySampler = CustomSampler.Create("Iterate - float[]");
		iterateNativeArraySampler = CustomSampler.Create("Iterate - NativeArray<float>");
		iterateNativeSliceSampler = CustomSampler.Create("Iterate - NativeSlice<float>");
    }

	unsafe void TestManagedArray()
	{
		setupSampler.Begin();
		var array = new float[PerformanceTestConfiguration.InstanceCount];
		setupSampler.End();

		iterateArraySampler.Begin();
		float sum = 0;
		for (int i = 0; i != array.Length; i++)
			sum += array[i];
		iterateArraySampler.End();
	}
	
	unsafe void TestNativeArray()
	{
		setupSampler.Begin();
		var array = new NativeArray<float>(PerformanceTestConfiguration.InstanceCount, Allocator.Persistent);
		setupSampler.End();

		iterateNativeArraySampler.Begin();
		{
			float sum = 0;
			for (int i = 0; i != array.Length; i++)
				sum += array[i];
		}
		iterateNativeArraySampler.End();
		

		iterateNativeSliceSampler.Begin();
		{
			var slice = new NativeSlice<float>(array);
			float sum = 0;
			for (int i = 0; i != slice.Length; i++)
				sum += slice[i];
		}
		iterateNativeSliceSampler.End();

		
		setupSampler.Begin();
		array.Dispose();
		setupSampler.End();
	}
	
	unsafe void TestUnsafePtr()
	{
		setupSampler.Begin();
		int size = sizeof(Component4Bytes) + sizeof(Component16Bytes)  + sizeof(Component64Bytes) + sizeof(Component12Bytes) + sizeof(Component128Bytes) + sizeof(Entity);
		size *= PerformanceTestConfiguration.InstanceCount;
		var src = (float*)UnsafeUtility.Malloc((ulong)size, 64, Allocator.Persistent);
		var dst = (float*)UnsafeUtility.Malloc((ulong)size, 64, Allocator.Persistent);
		setupSampler.End();

		memcpySampler.Begin();
		UnsafeUtility.MemCpy((IntPtr)dst, (IntPtr)src, (ulong)sizeof(float) * PerformanceTestConfiguration.InstanceCount);
		memcpySampler.End();

		instantiateMemcpySampler.Begin();
		UnsafeUtility.MemCpy((IntPtr)dst, (IntPtr)src, (ulong)size);
		instantiateMemcpySampler.End();

		iterateUnsafeSampler.Begin();
		float* ptr = src;
		float sum = 0;
		int count = PerformanceTestConfiguration.InstanceCount;
		for (int i = 0; i != count; i++)
			sum += ptr[i];
		iterateUnsafeSampler.End();
		
		setupSampler.Begin();
		UnsafeUtility.Free((IntPtr)src, Allocator.Persistent);
		UnsafeUtility.Free((IntPtr)dst, Allocator.Persistent);
		setupSampler.End();
	}
	
    unsafe void Update()
    {
	    TestUnsafePtr();
	    TestManagedArray();
	    TestNativeArray();
	    
		setupSampler.Begin();
        DependencyManager oldRoot = null;
        if (PerformanceTestConfiguration.CleanManagers)
        {
            oldRoot = DependencyManager.Root;
            DependencyManager.Root = new DependencyManager();
            DependencyManager.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount * 2);
        }

		var entityManager = DependencyManager.GetBehaviourManager<EntityManager>();

		setupSampler.End();
		
		setupSampler.Begin();
       	var archetype = entityManager.CreateEntity();
        entityManager.AddComponent<Component128Bytes>(archetype, new Component128Bytes());
        entityManager.AddComponent<Component12Bytes>(archetype, new Component12Bytes());
        entityManager.AddComponent<Component64Bytes>(archetype, new Component64Bytes());
	    entityManager.AddComponent<Component16Bytes>(archetype, new Component16Bytes());
	    entityManager.AddComponent<Component4Bytes>(archetype, new Component4Bytes());

        var group = entityManager.CreateComponentGroup(typeof(Component4Bytes));
		setupSampler.End();		
		
		instantiateSampler.Begin ();
        var instances = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount, Allocator.Temp);
		entityManager.Instantiate (archetype, instances);
		instantiateSampler.End();

		iterateSampler.Begin ();
		var array = group.GetComponentDataArray<Component4Bytes>();
		float sum = 0;
		for (int i = 0; i < array.Length; ++i)
			sum += array[i].value;
		iterateSampler.End ();

		destroySampler.Begin ();
		entityManager.DestroyEntity (instances);
		destroySampler.End ();

		setupSampler.Begin();
	    entityManager.DestroyEntity (archetype);
	    
		instances.Dispose ();
	    group.Dispose();
	    
        if (oldRoot != null)
        {
            DependencyManager.Root.Dispose();
            DependencyManager.Root = oldRoot;
        }
		setupSampler.End();
	}
}
