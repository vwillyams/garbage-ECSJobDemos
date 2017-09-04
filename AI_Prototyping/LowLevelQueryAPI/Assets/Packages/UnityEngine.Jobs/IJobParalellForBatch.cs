#if ENABLE_MANAGED_JOBS
using UnityEngine;
using System;
using UnityEngine.Jobs;

namespace UnityEngine.Jobs
{
    public interface IJobParallelForBatch
    {
        void Execute(int startIndex, int count);
    }

    public static class JobParallelForBatchExtensions
    {
        struct ParallelForBatchJobStruct<T> where T : struct, IJobParallelForBatch
        {
            static public IntPtr                            jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(T), (ExecuteJobFunction)Execute);
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref T data, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int beginIndex, int count);
            public static void Execute(ref T jobData, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int startIndex, int count)
            {
                jobData.Execute(startIndex, count);
            }
        }

        static public JobHandle ScheduleBatch<T>(this T jobData, int arrayLength, int minIndicesPerJobCount, JobHandle dependsOn = new JobHandle()) where T : struct, IJobParallelForBatch
        {
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobData), ParallelForBatchJobStruct<T>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, arrayLength, minIndicesPerJobCount);
        }

        static public void RunBatch<T>(this T jobData, int arrayLength) where T : struct, IJobParallelForBatch
        {
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref jobData), ParallelForBatchJobStruct<T>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, arrayLength, arrayLength);
        }
    }
}
#endif // ENABLE_MANAGED_JOBS
