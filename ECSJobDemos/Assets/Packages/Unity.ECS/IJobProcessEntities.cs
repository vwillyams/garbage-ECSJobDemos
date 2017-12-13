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
            fullData.array = array.m_Data;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, array.Length, innerloopBatchCount);
        }

        static public void Run<T, U0>(this T jobData, ComponentGroupArray<U0> array)
            where T : struct, IJobProcessEntities<U0>
            where U0 : struct
        {
            JobStruct<T, U0> fullData;
            fullData.data = jobData;
            fullData.array = array.m_Data;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), new JobHandle(), ScheduleMode.Run);
            int entityCount = array.Length;
            JobsUtility.ScheduleParallelFor(ref scheduleParams, entityCount , entityCount);
        }
        
        struct JobStruct<T, U0>
            where T : struct, IJobProcessEntities<U0>
            where U0 : struct
        {
            public ComponentGroupArrayData       array;
            public T                             data;

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
                var entity = default(U0);
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        if (begin < jobData.array.CacheBeginIndex || begin >= jobData.array.CacheEndIndex)
                            jobData.array.UpdateCache(begin);

                        int endLoop = Math.Min(end, jobData.array.CacheEndIndex); 
                        
                        for (int i = begin; i != endLoop; i++)
                        {
                            jobData.array.PatchPtrs(i, (byte*)UnsafeUtility.AddressOf(ref entity));
                            jobData.data.Execute(entity);
                        }

                        begin = endLoop;
                    }
                }
            }
        }
    }
}
