#if ENABLE_MANAGED_JOBS
using System;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Jobs
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

            public delegate void ExecuteJobFunction(int workerThreadIndex, ref T data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(int workerThreadIndex, ref T jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                JobsUtility.PrepareJobExecution(workerThreadIndex);
                while (true)
                {
                    int begin;
                    int end;
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                        return;

                    #if ENABLE_NATIVE_ARRAY_CHECKS
                    JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobData), begin, end - begin);
                    #endif

                    jobData.Execute(begin, end - begin);
                }
                JobsUtility.CleanupJobExecution();
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
