using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;
using UnityEngine.Assertions;

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
	[ComputeJobOptimization]
	struct Iterate_ComponentDataArray : IJob
	{
		public ComponentDataArray<Component4Bytes> array;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				float sum = 0;
				for (int i = 0; i < array.Length; ++i)
					sum += array[i].value;

				if (sum != 0.0F)
					throw new System.InvalidOperationException();
			}
		}
	}

	[ComputeJobOptimization]
	unsafe struct Iterate_FloatPointer : IJob
	{
		[NativeDisableUnsafePtrRestriction]
		public float* 	array;
		public int 	length;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				float sum = 0;
				for (int i = 0; i < length; ++i)
					sum += array[i];

				if (sum != 0.0F)
					throw new System.InvalidOperationException();
			}
		}
	}

	[ComputeJobOptimization]
	struct Iterate_NativeArray : IJob
	{
		public NativeArray<float> 	array;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				float sum = 0;
				for (int i = 0; i < array.Length; ++i)
					sum += array[i];

				if (sum != 0.0F)
					throw new System.InvalidOperationException();
			}
		}
	}

	[ComputeJobOptimization]
	struct Iterate_NativeSlice : IJob
	{
		public NativeSlice<float> 	slice;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				float sum = 0;
				for (int i = 0; i < slice.Length; ++i)
					sum += slice[i];

				if (sum != 0.0F)
					throw new System.InvalidOperationException();
			}
		}
	}
	
	CustomSampler setupSampler;
	CustomSampler instantiateSampler;
	CustomSampler destroySampler;
	CustomSampler iterateForEachSampler;
    CustomSampler memcpySampler;
	CustomSampler instantiateMemcpySampler ;
	CustomSampler iterateArraySampler;
	 
    void Awake()
	{
		setupSampler = CustomSampler.Create("Setup");
		instantiateSampler = CustomSampler.Create("InstantiateTest");
		instantiateMemcpySampler = CustomSampler.Create("InstantiateTest - Memcpy");
		destroySampler = CustomSampler.Create("DestroyTest");
		iterateForEachSampler = CustomSampler.Create("IterateTest - foreach() - ComponentGroupEnumerable<Component4Bytes*>");
        memcpySampler = CustomSampler.Create("Iterate - Memcpy");
		iterateArraySampler = CustomSampler.Create("Iterate - float[]");
    }

	unsafe void TestManagedArray()
	{
		setupSampler.Begin();
		var array = new float[PerformanceTestConfiguration.InstanceCount];
		setupSampler.End();

		iterateArraySampler.Begin();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			float sum = 0;
			for (int i = 0; i != array.Length; i++)
				sum += array[i];
			Assert.AreEqual(0.0F, sum);
		}
		iterateArraySampler.End();
	}
	
	unsafe void TestNativeArray()
	{
		setupSampler.Begin();
		var array = new NativeArray<float>(PerformanceTestConfiguration.InstanceCount, Allocator.Persistent);
		setupSampler.End();

		var jobArray = new Iterate_NativeArray() { array = array };
		jobArray.Run();

		var jobSlice = new Iterate_NativeSlice() { slice = array };
		jobSlice.Run();

		setupSampler.Begin();
		array.Dispose();
		setupSampler.End();
	}
	
	unsafe void TestUnsafePtr()
	{
		setupSampler.Begin();
		int size = sizeof(Component4Bytes) + sizeof(Component16Bytes)  + sizeof(Component64Bytes) + sizeof(Component12Bytes) + sizeof(Component128Bytes) + sizeof(Entity);
		size *= PerformanceTestConfiguration.InstanceCount;
		var src = (float*)UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
		UnsafeUtility.MemClear((IntPtr)src, size);
		
		var dst = (float*)UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
		setupSampler.End();

		instantiateMemcpySampler.Begin();
		UnsafeUtility.MemCpy((IntPtr)dst, (IntPtr)src, size);
		instantiateMemcpySampler.End();

		memcpySampler.Begin();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			UnsafeUtility.MemCpy((IntPtr)dst, (IntPtr)src, sizeof(float) * PerformanceTestConfiguration.InstanceCount);
		}
		memcpySampler.End();

		var jobFloatPtr = new Iterate_FloatPointer() { array = src, length = PerformanceTestConfiguration.InstanceCount };
		jobFloatPtr.Run();
		
		setupSampler.Begin();
		UnsafeUtility.Free((IntPtr)src, Allocator.Persistent);
		UnsafeUtility.Free((IntPtr)dst, Allocator.Persistent);
		setupSampler.End();
	}

	unsafe struct EntityIter
	{
		public Component4Bytes* component4Bytes;
	}

	unsafe void TestEntities()
	{
		setupSampler.Begin();
		World oldRoot = null;
		if (PerformanceTestConfiguration.CleanManagers)
		{
			oldRoot = World.Root;
			World.Root = new World();
			World.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount * 2);
		}

		var entityManager = World.GetBehaviourManager<EntityManager>();

		setupSampler.End();
		
		setupSampler.Begin();
		var archetype = entityManager.CreateEntity(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes), typeof(Component16Bytes), typeof(Component4Bytes));
		var group = entityManager.CreateComponentGroup(typeof(Component4Bytes));
		setupSampler.End();		
		
		instantiateSampler.Begin ();
		var instances = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount - 1, Allocator.Temp);
		entityManager.Instantiate (archetype, instances);
		instantiateSampler.End();

		var componentDataArrayJob = new Iterate_ComponentDataArray() { array = group.GetComponentDataArray<Component4Bytes>() };
		componentDataArrayJob.Run();

		var enumerator = new ComponentGroupArray<EntityIter>(entityManager);
			
		iterateForEachSampler.Begin ();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			float sum = 0;
			foreach (var e in enumerator)
				sum += e.component4Bytes->value;
			Assert.AreEqual(0.0F, sum);
		}
		iterateForEachSampler.End ();
		enumerator.Dispose();
		
		
		destroySampler.Begin ();
		entityManager.DestroyEntity (instances);
		destroySampler.End ();

		setupSampler.Begin();
		entityManager.DestroyEntity (archetype);
	    
		instances.Dispose ();
		group.Dispose();
	    
		if (oldRoot != null)
		{
			World.Root.Dispose();
			World.Root = oldRoot;
		}
		setupSampler.End();
	}


	unsafe void Update()
    {
	    TestUnsafePtr();
	    TestManagedArray();
	    TestNativeArray();
	    TestEntities();
    }
}
