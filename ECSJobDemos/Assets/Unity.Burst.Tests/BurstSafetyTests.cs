using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

public class BurstSafetyTests
{
    [ComputeJobOptimization(CompileSynchronously = true)]
    struct ThrowExceptionJob : IJobParallelFor
    {
        public void Execute(int index)
        {
            throw new System.ArgumentException("Blah");
        }
    }

    [Test]
    public void ThrowException()
    {
        LogAssert.Expect(LogType.Exception, new Regex("ArgumentException: Blah"));

        var jobData = new ThrowExceptionJob();
        jobData.Schedule(100, 1).Complete();
    }

    [ComputeJobOptimization(CompileSynchronously = true)]
    struct AccessNullNativeArrayJob : IJob
    {
        public void Execute()
        {
            var array = new NativeArray<float>();
            array[0] = 5;
        }
    }

    [Test]
    [Ignore("Crashing")]
    public void AccessNullNativeArray()
    {
        LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

        new AccessNullNativeArrayJob().Run();
    }

    [ComputeJobOptimization(CompileSynchronously = true)]
    unsafe struct AccessNullUnsafePtrJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] float* myArray;

        public void Execute()
        {
            myArray[0] = 5;
        }
    }

    [Test]
    [Ignore("Crashing")]
    public void AccessNullUnsafePtr()
    {
        LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

        new AccessNullUnsafePtrJob().Run();
    }
}