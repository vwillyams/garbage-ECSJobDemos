﻿using UnityEngine;
using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Unity.Entities.Tests;

public class BlobTests : ECSTestsFixture
{
	//@TODO: Test Prevent NativeArray and other containers inside of Blob data
	//@TODO: Test Prevent BlobPtr, BlobArray onto job struct
	//@TODO: Null check for BlobPtr
	//@TODO: Various tests trying to break the Allocator. eg. mix multiple BlobAllocator in the same BlobRoot...
	//@TODO: By default initialize blob data to zero

	struct MyData
	{
		public BlobArray<float> 			floatArray;
		public BlobPtr<float> 				nullPtr;
		public BlobPtr<Vector3> 			oneVector3;
		public float 						embeddedFloat;
		public BlobArray<BlobArray<int> > 	nestedArray;
	}

	// Creates a blob with a single C++ Allocator
	// * BlobRootPtr is reloctable without ptr patching
	// * BlobRootPtr<> can be used on a job and represents a single "container", it can as a whole be marked [ReadOnly]
	static unsafe BlobAssetReference<MyData> ConstructBlobData()
	{
		var allocator = new BlobAllocator(-1);

		ref var root = ref allocator.ConstructRoot<MyData>();

		allocator.Allocate(3, ref root.floatArray);
		allocator.Allocate(ref root.oneVector3);
		allocator.Allocate(2, ref root.nestedArray);

	    ref var nestedArrays = ref root.nestedArray;
		allocator.Allocate(1, ref nestedArrays[0]);
		allocator.Allocate(2, ref nestedArrays[1]);

		nestedArrays[0][0] = 0;
		nestedArrays[1][0] = 1;
		nestedArrays[1][1] = 2;

		root.floatArray[0] = 0;
		root.floatArray[1] = 1;
		root.floatArray[2] = 2;

		root.embeddedFloat = 4;
		root.oneVector3.Value = new Vector3 (3, 3, 3);

		var assetRef = allocator.CreateBlobAssetReference<MyData>(Allocator.Persistent);

	    allocator.Dispose();

	    return assetRef;
	}

    unsafe static void ValidateBlobData(ref MyData root)
    {
        Assert.AreEqual (3, root.floatArray.Length);
        Assert.AreEqual (0, root.floatArray[0]);
        Assert.AreEqual (1, root.floatArray[1]);
        Assert.AreEqual (2, root.floatArray[2]);
        Assert.AreEqual (new Vector3(3, 3, 3), root.oneVector3.Value);
        Assert.AreEqual (4, root.embeddedFloat);

        Assert.AreEqual (1, root.nestedArray[0].Length);
        Assert.AreEqual (2, root.nestedArray[1].Length);

        Assert.AreEqual (0, root.nestedArray[0][0]);
        Assert.AreEqual (1, root.nestedArray[1][0]);
        Assert.AreEqual (2, root.nestedArray[1][1]);
    }


    [Test]
	public unsafe void CreateBlobData()
	{
		var blob = ConstructBlobData ();
	    ValidateBlobData(ref blob.Value);

		blob.Release();
	}

	[Test]
	public unsafe void BlobAccessAfterReleaseThrows()
	{
		var blob = ConstructBlobData ();
		blob.Release();
	    Assert.Throws<InvalidOperationException>(() => { blob.GetUnsafePtr(); });
	}

	struct ValidateBlobJob : IJob
	{
	    //@TODO: BlobAsset should always be guranteed immutable
	    [ReadOnly]
		public BlobAssetReference<MyData> blob;

		public unsafe void Execute()
		{
		    ValidateBlobData(ref blob.Value);
		}
	}

    struct ComponentWithBlobData : IComponentData
    {
        public BlobAssetReference<MyData> blobAsset;
    }

	[Test]
	public void ReadBlobDataFromJob()
	{
		var blob = ConstructBlobData ();

		var jobData = new ValidateBlobJob();
		jobData.blob = blob;

		jobData.Schedule ().Complete();

		blob.Release();
	}


    struct ValidateBlobInComponentJob : IJobProcessComponentData<ComponentWithBlobData>
    {
        public bool ExpectException;

        public unsafe void Execute(ref ComponentWithBlobData component)
        {
            if (ExpectException)
            {
                var asset = component.blobAsset;
                Assert.Throws<InvalidOperationException>(() => { asset.GetUnsafePtr(); });
            }
            else
            {
                ValidateBlobData(ref component.blobAsset.Value);
            }
        }
    }

    class DummySystem : JobComponentSystem
    {
        
    }

    [Test]
	public unsafe void ParallelBlobAccessFromEntityJob()
	{
		var blob = CreateBlobEntities();

	    var jobData = new ValidateBlobInComponentJob();
	    
	    var system = World.Active.GetOrCreateManager<DummySystem>();
	    var jobHandle = jobData.Schedule(system, 1);

	    ValidateBlobData(ref blob.Value);

	    jobHandle.Complete ();

		blob.Release();
	}

    [Test]
    public void DestroyedBlobAccessFromEntityJobThrows()
    {
        var blob = CreateBlobEntities();

        blob.Release();

        var jobData = new ValidateBlobInComponentJob();
        jobData.ExpectException = true;
        var system = World.Active.GetOrCreateManager<DummySystem>();
        var jobHandle = jobData.Schedule(system, 1);

        jobHandle.Complete ();
    }


    BlobAssetReference<MyData> CreateBlobEntities()
    {
        var blob = ConstructBlobData();

        for (int i = 0; i != 32; i++)
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity, new ComponentWithBlobData() {blobAsset = blob});
        }
        return blob;
    }
}
