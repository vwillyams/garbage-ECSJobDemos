using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;

// 64 + 16 + 12 + 128 + 4 + 4 = 228 bytes

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

public class ECSIteratePerformance : MonoBehaviour
{
	unsafe struct EntityIter
	{
		public Component4Bytes* 	src;
		public Component4BytesDst* 	dst;
	}

	[ComputeJobOptimization]
	struct Iterate_ComponentDataFromEntity : IJob
	{
		public NativeArray<Entity> 							entities;
		public ComponentDataFromEntity<Component4Bytes> 	src;
		public ComponentDataFromEntity<Component4BytesDst> 	dst;

		public void Execute()
		{
			for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			{
				//@TODO: Ref returns upgrade
				for (int i = 0; i < entities.Length; ++i)
				{
					var entity = entities[i];
					dst[entity] = new Component4BytesDst(src[entity].value);
				}
			}
		}
	}

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
	struct Iterate_ProcessEntities : IJobProcessEntities<EntityIter>
	{
		unsafe public void Execute(EntityIter entity)
		{
			entity.dst->value = entity.src->value;
		}
	}

	//@TODO: Burst can't do this yet
	//[ComputeJobOptimization]
	struct Iterate_ForEachEntities : IJob
	{
		public ComponentGroupArray<EntityIter> entities;

		unsafe public void Execute()
		{
			foreach (var entity in entities)
				entity.dst->value = entity.src->value;
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
    CustomSampler memcpySampler;
	CustomSampler iterateArraySampler;

    void Awake()
	{
		setupSampler = CustomSampler.Create("Setup");
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

		int size = sizeof(float) * PerformanceTestConfiguration.InstanceCount;
		var src = (float*)UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
		UnsafeUtility.MemClear(src, size);

		var dst = (float*)UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
		setupSampler.End();

		memcpySampler.Begin();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
		{
			UnsafeUtility.MemCpy(dst, src, sizeof(float) * PerformanceTestConfiguration.InstanceCount);
		}
		memcpySampler.End();

		var jobFloatPtr = new Iterate_FloatPointer() { src = src, dst = dst, length = PerformanceTestConfiguration.InstanceCount };
		jobFloatPtr.Run();

		setupSampler.Begin();
		UnsafeUtility.Free(src, Allocator.Persistent);
		UnsafeUtility.Free(dst, Allocator.Persistent);
		setupSampler.End();
	}

	unsafe void TestEntities()
	{
		setupSampler.Begin();
		World oldRoot = null;
		if (PerformanceTestConfiguration.CleanManagers)
		{
			oldRoot = World.Active;
			World.Active = new World("IteratePerformance");
			World.Active.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount * 2);
		}

		var entityManager = World.Active.GetOrCreateManager<EntityManager>();

		var archetype = entityManager.CreateEntity(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes), typeof(Component16Bytes), typeof(Component4Bytes), typeof(Component4BytesDst));
		var group = entityManager.CreateComponentGroup(typeof(Component4Bytes), typeof(Component4BytesDst));

	    var instances = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount - 1, Allocator.Temp);

	    entityManager.Instantiate (archetype, instances);
	    setupSampler.End();

		var src = group.GetComponentDataArray<Component4Bytes>();
		var dst = group.GetComponentDataArray<Component4BytesDst>();

		var componentDataArrayJob = new Iterate_ComponentDataArray() { src = src, dst = dst };
		componentDataArrayJob.Run();

		var componentDataFromEntityJob = new Iterate_ComponentDataFromEntity() { src = entityManager.GetComponentDataFromEntity<Component4Bytes>(), dst = entityManager.GetComponentDataFromEntity<Component4BytesDst>(), entities = instances };
		componentDataFromEntityJob.Run();

		var componentProcessDataJob = new Iterate_ProcessComponentData();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			componentProcessDataJob.Run(src, dst);

		var entityArrayCache = new ComponentGroupArrayStaticCache(typeof(EntityIter), entityManager);
		var entityArray = new ComponentGroupArray<EntityIter>(entityArrayCache);

		var componentProcessEntities = new Iterate_ProcessEntities();
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			componentProcessEntities.Run(entityArray);

		var componentForEachEntities = new Iterate_ForEachEntities();
		componentForEachEntities.entities = entityArray;
		for (int iter = 0; iter != PerformanceTestConfiguration.Iterations; iter++)
			componentForEachEntities.Run();

		entityArrayCache.Dispose();

	    setupSampler.Begin ();
		entityManager.DestroyEntity (instances);

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
	    var wasEnabled = JobsUtility.JobDebuggerEnabled;
	    JobsUtility.JobDebuggerEnabled = false;

	    TestUnsafePtr();
	    TestManagedArray();
	    TestNativeArray();
	    TestEntities();

	    JobsUtility.JobDebuggerEnabled = wasEnabled;
    }
}
