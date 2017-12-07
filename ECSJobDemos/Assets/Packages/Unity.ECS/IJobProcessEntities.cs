using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    public interface IJobProcessEntities<U0> where U0 : struct
    {
        void Execute(U0 entity);
    }
    
    public static class ProcessEntityJobExtension1
    {
        static public JobHandle Schedule<T, U0>(this T jobData, ComponentGroupArray<U0> array, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessEntities<U0>
            where U0 : struct
        {
            JobStruct<T, U0> fullData;
            fullData.data = jobData;
            fullData.array = array;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, fullData.array.Length, innerloopBatchCount);
        }

        static public void Run<T, U0>(this T jobData, ComponentGroupArray<U0> array)
            where T : struct, IJobProcessEntities<U0>
            where U0 : struct
        {
            JobStruct<T, U0> fullData;
            fullData.data = jobData;
            fullData.array = array;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), new JobHandle(), ScheduleMode.Run);
            int entityCount = fullData.array.Length;
            JobsUtility.ScheduleParallelFor(ref scheduleParams, entityCount , entityCount);
        }
        
        struct JobStruct<T, U0>
            where T : struct, IJobProcessEntities<U0>
            where U0 : struct
        {
            public ComponentGroupArray<U0>   array;
            public T                         data;

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobStruct<T, U0> data, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref JobStruct<T, U0> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    for (int i = begin; i != end; i++)
                    {
                        //@TODO: Optimize into two loops to avoid branches inside indexer...
                        //@TODO: use ref returns to pass by ref instead of double copy
                        U0 value = jobData.array[i];
                        jobData.data.Execute(value);
                    }
                }
            }
        }
    }
}
