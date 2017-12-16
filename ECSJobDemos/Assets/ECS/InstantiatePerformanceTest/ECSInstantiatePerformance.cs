using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.ECS;
using UnityEngine.Profiling;
using Unity.Jobs.LowLevel.Unsafe;

public class ECSInstantiatePerformance : MonoBehaviour
{
	CustomSampler setupSampler;
	CustomSampler instantiateSampler;
	CustomSampler destroySampler;
    CustomSampler memcpySampler;
    CustomSampler instantiateMemcpySampler ;
    CustomSampler instantiateMemcpyReplicateSampler ;

    void Awake()
	{
		setupSampler = CustomSampler.Create("Setup");
		instantiateSampler = CustomSampler.Create("InstantiateTest");
	    instantiateMemcpySampler = CustomSampler.Create("InstantiateTest - Memcpy");
	    instantiateMemcpyReplicateSampler = CustomSampler.Create("InstantiateTest - MemcpyReplicate");
		destroySampler = CustomSampler.Create("DestroyTest");
        memcpySampler = CustomSampler.Create("Iterate - Memcpy");
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
			World.Active = new World();
			World.Active.SetDefaultCapacity(PerformanceTestConfiguration.InstanceCount * 2);
		}

		var entityManager = World.Active.GetOrCreateManager<EntityManager>();

	    var archetype = entityManager.CreateEntity(typeof(Component128Bytes), typeof(Component12Bytes), typeof(Component64Bytes), typeof(Component16Bytes), typeof(Component4Bytes), typeof(Component4BytesDst));
		var group = entityManager.CreateComponentGroup(typeof(Component4Bytes), typeof(Component4BytesDst));

	    var instances = new NativeArray<Entity>(PerformanceTestConfiguration.InstanceCount - 1, Allocator.Temp);
		setupSampler.End();



	    for (int i = 0; i < PerformanceTestConfiguration.Iterations; i++)
	    {
	        instantiateSampler.Begin ();
	        entityManager.Instantiate (archetype, instances);
	        instantiateSampler.End();

	        destroySampler.Begin ();
	        entityManager.DestroyEntity (instances);
	        destroySampler.End ();
	    }

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
	    var wasEnabled = JobsUtility.JobDebuggerEnabled;
	    JobsUtility.JobDebuggerEnabled = false;

	    TestEntities();
        TestUnsafePtr();

	    JobsUtility.JobDebuggerEnabled = wasEnabled;
    }
}
