using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class HLVMTests
{
	[ComputeJobOptimization]
	public struct SimpleArrayAssignJob : IJob
	{
		public float value;
		public NativeArray<float> input;
		public NativeArray<float> output;

		public void Execute()
		{
			for (int i = 0; i != output.Length; i++)
				output[i] = i + value + input[i];
		}
	}
	[Test]
    public void SimpleFloatArrayAssignSimple()
    {
        var job = new SimpleArrayAssignJob();
        job.value = 10.0F;
        job.input = new NativeArray<float>(3, Allocator.Persistent);
        job.output = new NativeArray<float>(3, Allocator.Persistent);

        for (int i = 0;i != job.input.Length;i++)
            job.input[i] = 1000.0F * i;

        job.Schedule().Complete();

        Assert.AreEqual(3, job.output.Length);
        for (int i = 0;i != job.output.Length;i++)
            Assert.AreEqual(i + job.value + job.input[i], job.output[i]);

        job.input.Dispose();
        job.output.Dispose();
    }

	[ComputeJobOptimization]
	public struct SimpleArrayAssignForJob : IJobParallelFor
	{
		public float value;
		public NativeArray<float> input;
		public NativeArray<float> output;

		public void Execute(int i)
		{
			output[i] = i + value + input[i];
		}
	}
    [Test]
    public void SimpleFloatArrayAssignForEach()
    {
        var job = new SimpleArrayAssignForJob();
        job.value = 10.0F;
        job.input = new NativeArray<float>(1000, Allocator.Persistent);
        job.output = new NativeArray<float>(1000, Allocator.Persistent);

        for (int i = 0;i != job.input.Length;i++)
            job.input[i] = 1000.0F * i;

        job.Schedule(job.input.Length, 40).Complete();

        Assert.AreEqual(1000, job.output.Length);
        for (int i = 0; i != job.output.Length; i++)
            Assert.AreEqual(i + job.value + job.input[i], job.output[i]);

        job.input.Dispose();
        job.output.Dispose();
    }


	[ComputeJobOptimization]
	struct MallocTestJob : IJob
	{
		public void Execute()
		{
			System.IntPtr allocated = UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>() * 100, 4, Allocator.Persistent);
			UnsafeUtility.Free(allocated, Allocator.Persistent);
		}            

	}

	[Test]
	public void MallocTest()
	{
		var jobData = new MallocTestJob();
		jobData.Run();
	}


	[ComputeJobOptimization]
	struct ListCapacityJob : IJob
	{
        public NativeList<int> list;
		public void Execute()
		{
            list.Capacity = 100;
		}

	}

	[Test]
	public void ListCapacityJobTest()
	{
		var jobData = new ListCapacityJob() { list = new NativeList<int>(Allocator.TempJob) };
		jobData.Run();

		Assert.AreEqual(100, jobData.list.Capacity);
		Assert.AreEqual(0, jobData.list.Length);

        jobData.list.Dispose();
	}


	[ComputeJobOptimization]
	struct NativeListAssignValue : IJob
	{
		public NativeList<int> list;

		public void Execute()
		{
			list[0] = 1;
		}
	}
	[Test]
	public void AssignValue()
	{
		var jobData = new NativeListAssignValue() { list = new NativeList<int>(Allocator.TempJob) };
		jobData.list.Add(5);

		jobData.Run();

		Assert.AreEqual(1, jobData.list.Length);
		Assert.AreEqual(1, jobData.list[0]);

		jobData.list.Dispose();
	}

	[ComputeJobOptimization]
	struct NativeListAddValue : IJob
	{
		public NativeList<int> list;

		public void Execute()
		{
			list.Add(1);
			list.Add(2);
		}
	}
	[Test]
	public void AddValue()
	{
		var jobData = new NativeListAddValue() { list = new NativeList<int>(1, Allocator.Persistent) };

		Assert.AreEqual(1, jobData.list.Capacity);
		jobData.list.Add(-1);

		jobData.Run();

		Assert.AreEqual(3, jobData.list.Length);
		Assert.AreEqual(-1, jobData.list[0]);
		Assert.AreEqual(1, jobData.list[1]);
		Assert.AreEqual(2, jobData.list[2]);

		jobData.list.Dispose();
	}


	[ComputeJobOptimization]
	struct ThrowExceptionJob : IJob
	{
        int valuel;
		public void Execute()
		{
			DoStuff(valuel);
		}

        void DoStuff(int value)
        { 
            throw new System.InvalidOperationException(string.Format("Boing {0}", valuel));
        }
	}

	[Test]
	public void ThrowException()
	{
        if (UnityEngineInternal.Jobs.JobCompiler.JitCompile != null && JobsUtility.GetAllowUsingJobCompiler())
            LogAssert.Expect(LogType.Error, "C# Compute: newobj:InvalidOperationException(InvalidOperationException::.ctor, call:string(string::Format, ldstr:string(\"Boing {0}\"), box:object([mscorlib]System.Int32, ldfld:int32(ThrowExceptionJob::valuel, ldloc:valuetype P2GCTests/ThrowExceptionJob&(this)))))! Please disable P2GC on the job for a more accurate error.");
        else
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: Boing 0");

        var jobData = new ThrowExceptionJob();
        jobData.Run();
	}
}