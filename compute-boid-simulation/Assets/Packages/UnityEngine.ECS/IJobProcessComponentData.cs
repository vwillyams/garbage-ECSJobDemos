using System;
using UnityEngine.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Experimental
{
    public interface IJobProcessComponentData<T0, T1>
        where T0 : struct, IComponentData
        where T1 : struct, IComponentData
    {
        void Execute(ref T0 data0, ref T1 data1);
    }

    public interface IJobProcessComponentData<T0>
        where T0 : struct, IComponentData
    {
        void Execute(ref T0 data);
    }

    public static class ProcessEntityJobExtension1
    {
        static public JobHandle Schedule<T, U0>(this T jobData, ComponentDataArray<U0> componentDataArray, int minIndicesPerJob, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            JobStruct<T, U0> fullData;
            fullData.data = jobData;
            fullData.componentDataArray = componentDataArray;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray.Length, minIndicesPerJob);
        }

        struct JobStruct<T, U0>
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            public ComponentDataArray<U0> componentDataArray;
            public T data;

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0>), typeof(T), (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobStruct<T, U0> data, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int beginIndex, int count);

            // @TODO: Use parallel for job... (Need to expose combine jobs)

            public unsafe static void Execute(ref JobStruct<T, U0> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int startIndex, int count)
            {
                int endIndex = startIndex + count;
                for (int i = startIndex; i != endIndex; i++)
                {
                    //@TODO: Optimize into two loops to avoid branches inside indexer...
                    //@TODO: use ref returns to pass by ref instead of double copy

                    U0 value = jobData.componentDataArray[i];
                    jobData.data.Execute(ref value);
                    jobData.componentDataArray[i] = value;
                }
            }
        }
    }

    public static class ProcessEntityJobExtension2
    {
        static public JobHandle Schedule<T, U0, U1>(this T jobData, ComponentDataArray<U0> componentDataArray0, ComponentDataArray<U1> componentDataArray1, int minIndicesPerJob, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            JobStruct<T, U0, U1> fullData;
            fullData.data = jobData;
            fullData.componentDataArray0 = componentDataArray0;
            fullData.componentDataArray1 = componentDataArray1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0, U1>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, minIndicesPerJob);
        }

        struct JobStruct<T, U0, U1>
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            public ComponentDataArray<U0> componentDataArray0;
            public ComponentDataArray<U1> componentDataArray1;
            public T data;

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0, U1>), typeof(T), (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobStruct<T, U0, U1> data, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int beginIndex, int count);

            // @TODO: Use parallel for job... (Need to expose combine jobs)

            public unsafe static void Execute(ref JobStruct<T, U0, U1> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, int startIndex, int count)
            {
                int endIndex = startIndex + count;
                for (int i = startIndex; i != endIndex; i++)
                {
                    //@TODO: Optimize into two loops to avoid branches inside indexer...
                    //@TODO: use ref returns to pass by ref instead of double copy

                    U0 value0 = jobData.componentDataArray0[i];
                    U1 value1 = jobData.componentDataArray1[i];
                    jobData.data.Execute(ref value0, ref value1);
                    jobData.componentDataArray0[i] = value0;
                    jobData.componentDataArray1[i] = value1;
                }
            }
        }
    }
}