using System;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Jobs
{
    [JobProducerType(typeof(JobParallelIndexListExtensions.JobStructProduce<>))]
    public interface IJobForEach
    {
        bool Execute(int index);
    }

    public static class JobForEachExtensions
    {
        public struct JobStructProduce<T> where T : struct, IJobForEach
        {
            public struct JobDataWithFiltering
            {
                public T data;
                public int arrayLength;
            }

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobDataWithFiltering), typeof(T), JobType.Single, (ExecuteJobFunction) Execute);
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobDataWithFiltering data, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref JobDataWithFiltering jobData, System.IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobData), 0, jobData.arrayLength);
#endif
                for (int i = 0; i != jobData.arrayLength; i++)
                {
                    jobData.data.Execute(i);
                }
            }
        }

        unsafe static public JobHandle Schedule<T>(this T jobData, int arrayLength, JobHandle dependsOn = new JobHandle()) where T : struct, IJobForEach
        {
            JobStructProduce<T>.JobDataWithFiltering fullData;
            fullData.data = jobData;
            fullData.arrayLength = arrayLength;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStructProduce<T>.Initialize(), dependsOn, ScheduleMode.Batched);

            return JobsUtility.Schedule(ref scheduleParams);
        }
    }
}
