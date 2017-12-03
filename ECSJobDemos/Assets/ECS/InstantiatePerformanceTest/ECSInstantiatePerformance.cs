using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;
using Unity.Jobs.LowLevel.Unsafe;
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

struct Component4BytesDst : IComponentData
{
	public float value;

	public Component4BytesDst(float v)
	{
		value = v;
	}
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
		public ComponentDataArray<Component4Bytes> 		src;
		public ComponentDataArray<Component4BytesDst> 	dst;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				//@TODO: Ref returns upgrade
				for (int i = 0; i < src.Length; ++i)
					dst[i] = new Component4BytesDst(src[i].value);
			}
		}
	}
	
	
	[ComputeJobOptimization]
	struct Iterate_ProcessComponentData : IJobProcessComponentData<Component4Bytes, Component4BytesDst>
	{
		public void Execute(ref Component4Bytes src, ref Component4BytesDst dst)
		{
			dst.value = src.value;
		}
	}

	[ComputeJobOptimization]
	unsafe struct Iterate_FloatPointer : IJob
	{
		[NativeDisableUnsafePtrRestriction]
		public float* 	src;
		[NativeDisableUnsafePtrRestriction]
		public float* 	dst;
		public int 		length;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				for (int i = 0; i < length; ++i)
					dst[i] = src[i];
			}
		}
	}

	[ComputeJobOptimization]
	struct Iterate_NativeArrayParallelFor : IJobParallelFor
	{
		[NativeMatchesParallelForLength]
		public NativeArray<float> 	src;
		
		[NativeMatchesParallelForLength]
		public NativeArray<float> 	dst;
		
		public void Execute(int i)
		{
			dst[i] = src[i];
		}
	}
	
	[ComputeJobOptimization]
	struct Iterate_NativeArray : IJob
	{
		public NativeArray<float> 	src;
		public NativeArray<float> 	dst;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				for (int i = 0; i < dst.Length; ++i)
					dst[i] = src[i];
			}
		}
	}

	[ComputeJobOptimization]
	struct Iterate_NativeSlice : IJob
	{
		public NativeSlice<float> 	src;
		public NativeSlice<float> 	dst;
		
		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				for (int i = 0; i < dst.Length; ++i)
					dst[i] = src[i];
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
		var src = new float[PerformanceTestConfiguration.InstanceCount];
		var dst = new float[PerformanceTestConfiguration.InstanceCount];
		setupSampler.End();

		iterateArraySampler.Begin();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			for (int i = 0; i != src.Length; i++)
				dst[i] = src[i];
		}
		iterateArraySampler.End();
	}
	
	unsafe void TestNativeArray()
	{
		setupSampler.Begin();
		var src = new NativeArray<float>(PerformanceTestConfiguration.InstanceCount, Allocator.Persistent);
		var dst = new NativeArray<float>(PerformanceTestConfiguration.InstanceCount, Allocator.Persistent);
		setupSampler.End();

		var jobArray = new Iterate_NativeArray() { src = src, dst = dst };
		jobArray.Run();

		var jobArrayParallellFor = new Iterate_NativeArrayParallelFor() { src = src, dst = dst };
		for (int i = 0;i != PerformanceTestConfiguration.Iterations;i++)
			jobArrayParallellFor.Run(src.Length);
		
		var jobSlice = new Iterate_NativeSlice() { src = src, dst = dst };
		jobSlice.Run();

		setupSampler.Begin();
		src.Dispose();
		dst.Dispose();
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

		var jobFloatPtr = new Iterate_FloatPointer() { src = src, dst = dst, length = PerformanceTestConfiguration.InstanceCount };
		jobFloatPtr.Run();
		
		setupSampler.Begin();
		UnsafeUtility.Free((IntPtr)src, Allocator.Persistent);
		UnsafeUtility.Free((IntPtr)dst, Allocator.Persistent);
		setupSampler.End();
	}

	unsafe struct EntityIter
	{
		public Component4Bytes* 	src;
		public Component4BytesDst* 	dst;
	}

	unsafe void TestEntities()
	{
		setupSampler.Begin();
		World oldRoot = null;
		if (PerformanceTestConfiguration.CleanManagers)
		{
			oldRoot = World.Active;
			World.Active = new World();
			World.Active.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount * 2);
		}

		var entityManager = World.Active.GetOrCreateManager<EntityManager>();

		setupSampler.End();
		
		setupSampler.Begin();
		var archetype = entityManager.CreateEntity(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes), typeof(Component16Bytes), typeof(Component4Bytes), typeof(Component4BytesDst));
		var group = entityManager.CreateComponentGroup(typeof(Component4Bytes), typeof(Component4BytesDst));
		setupSampler.End();		
		
		instantiateSampler.Begin ();
		var instances = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount - 1, Allocator.Temp);
		entityManager.Instantiate (archetype, instances);
		instantiateSampler.End();

		var src = group.GetComponentDataArray<Component4Bytes>();
		var dst = group.GetComponentDataArray<Component4BytesDst>();

		var componentDataArrayJob = new Iterate_ComponentDataArray() { src = src, dst = dst };
		componentDataArrayJob.Run();

		var componentProcessDataJob = new Iterate_ProcessComponentData();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			componentProcessDataJob.Run(src, dst);
		}
		
		var enumerator = new ComponentGroupArray<EntityIter>(entityManager);
			
		iterateForEachSampler.Begin ();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			foreach (var e in enumerator)
				e.dst->value = e.src->value;
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
			World.Active.Dispose();
			World.Active = oldRoot;
		}
		setupSampler.End();
	}


	unsafe void Update()
    {
	    var wasEnabled = JobsUtility.GetJobDebuggerEnabled();
	    JobsUtility.SetJobDebuggerEnabled(false);

	    TestUnsafePtr();
	    TestManagedArray();
	    TestNativeArray();
	    TestEntities();
	    
	    JobsUtility.SetJobDebuggerEnabled(wasEnabled);
    }
}
