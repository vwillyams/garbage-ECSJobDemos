#define PROCESS_LOOP_BURST_WORKAROUND

using System;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace UnityEngine.ECS
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

    public interface IAutoComponentSystemJob
    {
        void Prepare();
    }

    public static class JobProcessComponentDataExtension1
    {
        static public JobHandle Schedule<T, U0>(this T jobData, ComponentDataArray<U0> componentDataArray, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            JobStruct<T, U0> fullData;
            fullData.data = jobData;
            fullData.componentDataArray = componentDataArray;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray.Length, innerloopBatchCount);
        }

        static public void Run<T, U0>(this T jobData, ComponentDataArray<U0> componentDataArray)
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            JobStruct<T, U0> fullData;
            fullData.data = jobData;
            fullData.componentDataArray = componentDataArray;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray.Length, componentDataArray.Length);
        }
        
        struct JobStruct<T, U0>
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  componentDataArray;
            public T                       data;

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0>), typeof(T), (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobStruct<T, U0> data, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            #if PROCESS_LOOP_BURST_WORKAROUND
            public unsafe static void Execute(ref JobStruct<T, U0> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    for (int i = begin; i != end; i++)
                    {
                        //@TODO: use ref returns to pass by ref instead of double copy
                        var value = jobData.componentDataArray[i];
                        jobData.data.Execute(ref value);
                        jobData.componentDataArray[i] = value;
                    }
                }
            }
            #else
            public unsafe static void Execute(ref JobStruct<T, U0> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        var array = jobData.componentDataArray.GetChunkArray(begin, end - begin);
                        
                        for (int i = 0; i != array.Length; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value = array[i];
                            jobData.data.Execute(ref value);
                            array[i] = value;
                        }

                        begin += array.Length;
                    }
                }
            }            
            #endif
        }
    }

    public static class JobProcessComponentDataExtension2
    {
        static public JobHandle Schedule<T, U0, U1>(this T jobData, ComponentDataArray<U0> componentDataArray0, ComponentDataArray<U1> componentDataArray1, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            JobStruct<T, U0, U1> fullData;
            fullData.data = jobData;
            fullData.componentDataArray0 = componentDataArray0;
            fullData.componentDataArray1 = componentDataArray1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0, U1>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, innerloopBatchCount);
        }
        
        static public void Run<T, U0, U1>(this T jobData, ComponentDataArray<U0> componentDataArray0, ComponentDataArray<U1> componentDataArray1)
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            JobStruct<T, U0, U1> fullData;
            fullData.data = jobData;
            fullData.componentDataArray0 = componentDataArray0;
            fullData.componentDataArray1 = componentDataArray1;
            
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0, U1>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, componentDataArray0.Length);
        }

        struct JobStruct<T, U0, U1>
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  componentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U1>  componentDataArray1;
            public T                       data;

            static public IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0, U1>), typeof(T), (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JobStruct<T, U0, U1> data, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            #if PROCESS_LOOP_BURST_WORKAROUND
            public unsafe static void Execute(ref JobStruct<T, U0, U1> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    for (int i = begin; i != end; i++)
                    {
                        //@TODO: use ref returns to pass by ref instead of double copy
                        var value0 = jobData.componentDataArray0[i];
                        var value1 = jobData.componentDataArray1[i];
                        jobData.data.Execute(ref value0, ref value1);
                        jobData.componentDataArray0[i] = value0;
                        jobData.componentDataArray1[i] = value1;
                    }
                }
            }
    #else
            public unsafe static void Execute(ref JobStruct<T, U0, U1> jobData, IntPtr additionalPtr, System.IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        var array0 = jobData.componentDataArray0.GetChunkArray(begin, end - begin);
                        var array1 = jobData.componentDataArray1.GetChunkArray(begin, end - begin);
                        
                        for (int i = 0; i != array0.Length; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = array0[i];
                            var value1 = array1[i];
                            jobData.data.Execute(ref value0, ref value1);
                            array0[i] = value0;
                            array1[i] = value1;
                        }

                        begin += array0.Length;
                    }
                }
            }     
    #endif
        }
    }

    public class GenericProcessComponentSystem<TJob, TComponentData0> : JobComponentSystem 
        where TJob : struct, IAutoComponentSystemJob, IJobProcessComponentData<TComponentData0>
        where TComponentData0 : struct, IComponentData
    {
        struct DataGroup
        {
            internal ComponentDataArray<TComponentData0> component0;
        }
        [InjectComponentGroup]
        private DataGroup m_Group;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            int batchSize = 32;

            TJob jobData = default(TJob);
            jobData.Prepare();

            return jobData.Schedule(m_Group.component0, batchSize, inputDeps);
        }
    }

    public class GenericProcessComponentSystem<TJob, TComponentData0, TComponentData1> : JobComponentSystem
    where TJob : struct, IAutoComponentSystemJob, IJobProcessComponentData<TComponentData0, TComponentData1>
    where TComponentData0 : struct, IComponentData
    where TComponentData1 : struct, IComponentData
    {
        struct DataGroup
        {
            internal ComponentDataArray<TComponentData0> component0;
            internal ComponentDataArray<TComponentData1> component1;
        }

        [InjectComponentGroup]
        private DataGroup m_Group;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            int batchSize = 32;

            TJob jobData = default(TJob);
            jobData.Prepare();

            return jobData.Schedule(m_Group.component0, m_Group.component1, batchSize, inputDeps);
        }
    }

}
