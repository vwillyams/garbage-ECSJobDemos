using System;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.ECS;

namespace Unity.ECS
{
    public interface IAutoComponentSystemJob
    {
        void Prepare();
    }

    public interface IJobProcessComponentData<T0>
        where T0 : struct, IComponentData
    {
        void Execute(ref T0 data);
    }

    public interface IJobProcessComponentData<T0, T1>
        where T0 : struct, IComponentData
        where T1 : struct, IComponentData
    {
        void Execute(ref T0 data0, ref T1 data1);
    }

    public static class JobProcessComponentDataExtensions
    {
        public static unsafe JobHandle Schedule<T, U0>(this T jobData, ComponentDataArray<U0> componentDataArray, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            JobStruct<T, U0> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray = componentDataArray;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray.Length, innerloopBatchCount);
        }

        public static unsafe void Run<T, U0>(this T jobData, ComponentDataArray<U0> componentDataArray)
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            JobStruct<T, U0> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray = componentDataArray;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray.Length, componentDataArray.Length);
        }

        private struct JobStruct<T, U0>
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            private static IntPtr jobReflectionData;

            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  ComponentDataArray;
            public T                       Data;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            private delegate void ExecuteJobFunction(ref JobStruct<T, U0> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static unsafe void Execute(ref JobStruct<T, U0> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        var array = jobData.ComponentDataArray.GetChunkArray(begin, end - begin);
                        var ptr = array.GetUnsafePtr();

                        for (int i = 0; i != array.Length; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value = UnsafeUtility.ReadArrayElement<U0>(ptr, i);
                            jobData.Data.Execute(ref value);
                            UnsafeUtility.WriteArrayElement(ptr, i, value);
                        }

                        begin += array.Length;
                    }
                }
            }
        }

        public static unsafe JobHandle Schedule<T, U0, U1>(this T jobData, ComponentDataArray<U0> componentDataArray0, ComponentDataArray<U1> componentDataArray1, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            JobStruct<T, U0, U1> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = componentDataArray0;
            fullData.ComponentDataArray1 = componentDataArray1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0, U1>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, innerloopBatchCount);
        }

        public static unsafe void Run<T, U0, U1>(this T jobData, ComponentDataArray<U0> componentDataArray0, ComponentDataArray<U1> componentDataArray1)
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            JobStruct<T, U0, U1> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = componentDataArray0;
            fullData.ComponentDataArray1 = componentDataArray1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct<T, U0, U1>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, componentDataArray0.Length);
        }

        private struct JobStruct<T, U0, U1>
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            private static IntPtr jobReflectionData;

            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U1>  ComponentDataArray1;
            public T                       Data;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0, U1>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);

                return jobReflectionData;
            }

            private delegate void ExecuteJobFunction(ref JobStruct<T, U0, U1> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            private static unsafe void Execute(ref JobStruct<T, U0, U1> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        var array0 = jobData.ComponentDataArray0.GetChunkArray(begin, end - begin);
                        var array1 = jobData.ComponentDataArray1.GetChunkArray(begin, end - begin);
                        //@TODO: Currently Assert.AreEqual doens't compile in burst. Need to find out why...
                        // Assert.AreEqual(array0.Length, array1.Length);

                        var ptr0 = array0.GetUnsafePtr();
                        var ptr1 = array1.GetUnsafePtr();

                        for (int i = 0; i != array0.Length; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = UnsafeUtility.ReadArrayElement<U0>(ptr0, i);
                            var value1 = UnsafeUtility.ReadArrayElement<U1>(ptr1, i);

                            jobData.Data.Execute(ref value0, ref value1);

                            UnsafeUtility.WriteArrayElement(ptr0, i, value0);
                            UnsafeUtility.WriteArrayElement(ptr1, i, value1);
                        }

                        begin += array0.Length;
                    }
                }
            }
        }
    }

    public class GenericProcessComponentSystem<TJob, TComponentData0> : JobComponentSystem
        where TJob : struct, IAutoComponentSystemJob, IJobProcessComponentData<TComponentData0>
        where TComponentData0 : struct, IComponentData
    {
        private struct DataGroup
        {
            internal ComponentDataArray<TComponentData0> Component0;
        }

        [Inject]
        private DataGroup m_Group;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            const int batchSize = 32;

            var jobData = default(TJob);
            jobData.Prepare();

            return jobData.Schedule(m_Group.Component0, batchSize, inputDeps);
        }
    }

    public class GenericProcessComponentSystem<TJob, TComponentData0, TComponentData1> : JobComponentSystem
        where TJob : struct, IAutoComponentSystemJob, IJobProcessComponentData<TComponentData0, TComponentData1>
        where TComponentData0 : struct, IComponentData
        where TComponentData1 : struct, IComponentData
    {
        private struct DataGroup
        {
            internal ComponentDataArray<TComponentData0> Component0;
            internal ComponentDataArray<TComponentData1> Component1;
        }

        [Inject]
        private DataGroup m_Group;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            const int batchSize = 32;

            var jobData = default(TJob);
            jobData.Prepare();

            return jobData.Schedule(m_Group.Component0, m_Group.Component1, batchSize, inputDeps);
        }
    }
}
