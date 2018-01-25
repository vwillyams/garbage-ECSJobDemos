using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Jobs.LowLevel.Unsafe;

public class ECSInstantiatePerformance : MonoBehaviour
{
	CustomSampler setupSampler;
    CustomSampler instantiateSamplerBatch;
    CustomSampler destroySamplerBatch;

    CustomSampler createEntitySamplerBatch;
    CustomSampler destroyEntitySamplerBatch;

    CustomSampler instantiateSamplerSingle;
    CustomSampler destroySamplerSingle;


    CustomSampler instantiateMemcpySampler ;
    CustomSampler instantiateMemcpyReplicateSampler ;

    void Awake()
	{
		setupSampler = CustomSampler.Create("Setup");

	    instantiateSamplerBatch = CustomSampler.Create("InstantiateTest (Batch)");
	    destroySamplerBatch = CustomSampler.Create("DestroyTest (Batch)");

	    createEntitySamplerBatch = CustomSampler.Create("CreateEntityTest (Batch)");
	    destroyEntitySamplerBatch = CustomSampler.Create("DestroyEntityTest(Batch)");

	    instantiateSamplerSingle = CustomSampler.Create("InstantiateTest (Single)");
	    destroySamplerSingle = CustomSampler.Create("DestroyTest (Single)");

	    instantiateMemcpySampler = CustomSampler.Create("InstantiateTest - Memcpy");
	    instantiateMemcpyReplicateSampler = CustomSampler.Create("InstantiateTest - MemcpyReplicate");
    }

    unsafe void TestUnsafePtr()
    {
        setupSampler.Begin();
        int[] sizes =
        {
            sizeof(Component128Bytes), sizeof(Component12Bytes), sizeof(Component64Bytes), sizeof(Component16Bytes),
            sizeof(Component4Bytes), sizeof(Component4BytesDst), sizeof(Entity),
            20 // overhead of EntityData lookup table
        };

        int size = 0;
        foreach (var s in sizes)
            size += s;
        size *= PerformanceTestConfiguration.InstanceCount;
        var src = (float*)UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
        UnsafeUtility.MemClear(src, size);

        var dst = (float*)UnsafeUtility.Malloc(size, 64, Allocator.Persistent);
        setupSampler.End();

        instantiateMemcpySampler.Begin();
        for (int i = 0; i < PerformanceTestConfiguration.Iterations; i++)
            UnsafeUtility.MemCpy(dst, src, size);
        instantiateMemcpySampler.End();

        instantiateMemcpyReplicateSampler.Begin();
        for (int i = 0; i < PerformanceTestConfiguration.Iterations; i++)
        {
            int position = 0;
            foreach (var elementSize in sizes)
            {
                UnsafeUtility.MemCpyReplicate((byte*)dst + position, src, elementSize, PerformanceTestConfiguration.InstanceCount);
                position += elementSize * PerformanceTestConfiguration.InstanceCount;
            }

        }
        instantiateMemcpyReplicateSampler.End();


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
			World.Active = new World("InstantiatePerformance");
			World.Active.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount * 2);
		}

		var entityManager = World.Active.GetOrCreateManager<EntityManager>();

	    var archetype = entityManager.CreateArchetype(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes), typeof(Component16Bytes), typeof(Component4Bytes), typeof(Component4BytesDst));
	    var srcEntity = entityManager.CreateEntity(archetype );
		var group = entityManager.CreateComponentGroup(typeof(Component4Bytes), typeof(Component4BytesDst));

	    var instances = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount - 1, Allocator.Temp);
		setupSampler.End();



	    for (int i = 0; i < PerformanceTestConfiguration.Iterations; i++)
	    {
	        instantiateSamplerBatch.Begin ();
	        entityManager.Instantiate (srcEntity, instances);
	        instantiateSamplerBatch.End();

	        destroySamplerBatch.Begin ();
	        entityManager.DestroyEntity (instances);
	        destroySamplerBatch.End ();
	    }


	    for (int i = 0; i < PerformanceTestConfiguration.Iterations; i++)
	    {
	        instantiateSamplerSingle.Begin ();
	        for (int k=0;k != instances.Length;k++)
	            instances[k] = entityManager.Instantiate (srcEntity);
	        instantiateSamplerSingle.End();

	        destroySamplerSingle.Begin ();
	        for (int k=0;k != instances.Length;k++)
	            entityManager.DestroyEntity (instances[k]);
	        destroySamplerSingle.End ();
	    }

	    setupSampler.Begin();
	    entityManager.DestroyEntity (srcEntity);
	    setupSampler.End();

	    for (int i = 0; i < PerformanceTestConfiguration.Iterations; i++)
	    {
	        createEntitySamplerBatch.Begin ();
	        entityManager.CreateEntity(archetype, instances);
	        createEntitySamplerBatch.End();

	        destroyEntitySamplerBatch.Begin ();
	        entityManager.DestroyEntity (instances);
	        destroyEntitySamplerBatch.End ();
	    }

	    setupSampler.Begin();

	    
	    
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

	    TestEntities();
        TestUnsafePtr();

	    JobsUtility.JobDebuggerEnabled = wasEnabled;
    }
}
