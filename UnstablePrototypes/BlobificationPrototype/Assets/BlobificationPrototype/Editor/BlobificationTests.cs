using UnityEngine;
using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Jobs;

public class BlobTests
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

		MyData* root = (MyData*)allocator.ConstructRoot<MyData>();

		allocator.Allocate(3, ref root->floatArray);
		allocator.Allocate(ref root->oneVector3);
		allocator.Allocate(2, ref root->nestedArray);

		BlobArray<int>* nestedArrays = (BlobArray<int>*)root->nestedArray.UnsafePtr;
		allocator.Allocate(1, ref nestedArrays[0]);
		allocator.Allocate(2, ref nestedArrays[1]);

		nestedArrays[0][0] = 0;
		nestedArrays[1][0] = 1;
		nestedArrays[1][1] = 2;

		root->floatArray[0] = 0;
		root->floatArray[1] = 1;
		root->floatArray[2] = 2;

		root->embeddedFloat = 4;
		root->oneVector3.Value = new Vector3 (3, 3, 3);

		var assetRef = allocator.CreateBlobAssetReference<MyData>(Allocator.Persistent);

	    allocator.Dispose();

	    return assetRef;
	}

    unsafe static void ValidateBlobData(MyData* root)
    {
        Assert.AreEqual (3, root->floatArray.Length);
        Assert.AreEqual (0, root->floatArray[0]);
        Assert.AreEqual (1, root->floatArray[1]);
        Assert.AreEqual (2, root->floatArray[2]);
        Assert.AreEqual (new Vector3(3, 3, 3), root->oneVector3.Value);
        Assert.AreEqual (4, root->embeddedFloat);

        Assert.AreEqual (1, root->nestedArray[0].Length);
        Assert.AreEqual (2, root->nestedArray[1].Length);

        var nested = (BlobArray<int>*)(root->nestedArray.UnsafePtr);

        Assert.AreEqual (0, nested[0][0]);
        Assert.AreEqual (1, nested[1][0]);
        Assert.AreEqual (2, nested[1][1]);
    }


    [Test]
	public unsafe void CreateBlobData()
	{
		var blob = ConstructBlobData ();
		MyData* root = (MyData*)blob.GetUnsafeReadOnlyPtr();
	    ValidateBlobData(root);

		blob.Release();
	}

	[Test]
	[Ignore("Failing")]
	public unsafe void CachedBlobArrayThrowsExceptionAfterDeallocatingRoot()
	{
		var blob = ConstructBlobData ();
		MyData* root = (MyData*)blob.GetUnsafeReadOnlyPtr();
		var floatArray = root->floatArray;
		blob.Release();

		Assert.Throws<InvalidOperationException>(() => { var p = floatArray[0]; });
	}

	struct JobData : IJob
	{
	    //@TODO: BlobAsset should always be guranteed immutable
	    [ReadOnly]
		public BlobAssetReference<MyData> blob;

		public unsafe void Execute()
		{
			MyData* data = (MyData*)blob.GetUnsafeReadOnlyPtr();
		    ValidateBlobData(data);
		}
	}

	[Test]
	public void ReadBlobDataFromJob()
	{
		var blob = ConstructBlobData ();

		var jobData = new JobData();
		jobData.blob = blob;

		jobData.Schedule ().Complete();

		blob.Release();
	}

	[Test]
	public unsafe void ScheduleJobDebugger()
	{
		var blob = ConstructBlobData ();

		var jobData = new JobData();
		jobData.blob = blob;

		var jobHandle = jobData.Schedule ();

		Assert.Throws<InvalidOperationException>(() => { var p = blob.GetUnsafeReadOnlyPtr(); });

		jobHandle.Complete ();

		blob.Release();
	}

	[Test]
	[Ignore ("Not supported. Read only for the whole blob needs to be enforced via static code analysis.")]
	public unsafe void ScheduleJobDebuggerNotImplemented()
	{
		var blob = ConstructBlobData ();

		var jobData = new JobData();
		jobData.blob = blob;

		// Keep a reference to a float array
		var floatArray = ((MyData*)blob.GetUnsafeReadOnlyPtr())->floatArray;

		// Schedule job
		var jobHandle = jobData.Schedule ();

		// This should naturally throw an exception. Right now this can not be tracked.
		// Since the data in the blob is shared between all threads,
		// thus the whole per thread version masking machinery can't be used.
		Assert.Throws<InvalidOperationException>(() => { floatArray[0] = 5; });

		jobHandle.Complete ();

		blob.Release();
	}
}
