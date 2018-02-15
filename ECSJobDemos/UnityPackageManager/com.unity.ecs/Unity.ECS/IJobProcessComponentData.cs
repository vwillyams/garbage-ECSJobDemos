using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

namespace Unity.ECS
{
    public interface IAutoComponentSystemJob
    {
        void Prepare();
    }

    public interface IBaseJobProcessComponentData_2
    {
    }

    public interface IJobProcessComponentData<T0>
        where T0 : struct, IComponentData
    {
        void Execute(ref T0 data);
    }

    public interface IJobProcessComponentData<T0, T1> : IBaseJobProcessComponentData_2
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
        
        struct JobStruct<T, U0>
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            static IntPtr s_JobReflectionData;

            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  ComponentDataArray;
            public T                       Data;

            public static IntPtr Initialize()
            {
                if (s_JobReflectionData == IntPtr.Zero)
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct<T, U0>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);

                return s_JobReflectionData;
            }
            
            delegate void ExecuteJobFunction(ref JobStruct<T, U0> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct<T, U0> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
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
            JobStruct_Process2<T, U0, U1> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = componentDataArray0;
            fullData.ComponentDataArray1 = componentDataArray1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_Process2<T, U0, U1>.Initialize(), dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, innerloopBatchCount);
        }

        public static unsafe void Run<T, U0, U1>(this T jobData, ComponentDataArray<U0> componentDataArray0, ComponentDataArray<U1> componentDataArray1)
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            JobStruct_Process2<T, U0, U1> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = componentDataArray0;
            fullData.ComponentDataArray1 = componentDataArray1;

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_Process2<T, U0, U1>.Initialize(), new JobHandle(), ScheduleMode.Run);
            JobsUtility.ScheduleParallelFor(ref scheduleParams, componentDataArray0.Length, componentDataArray0.Length);
        }
        
        
        public static unsafe JobHandle Schedule<T>(this T jobData, ComponentSystemBase componentSystem, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IBaseJobProcessComponentData_2
        {
            ComponentType* types = stackalloc ComponentType[2];
            var jobReflection = JobStruct_ProcessInfer<T>.Initialize(out types[0], out types[1]);

            var group = componentSystem.GetComponentGroup(types, 2);
            
            JobStruct_ProcessInfer<T> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = group.GetComponentDataArray<ProxyComponentData>(types[0]);
            fullData.ComponentDataArray1 = group.GetComponentDataArray<ProxyComponentData>(types[1]);

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), jobReflection, dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, fullData.ComponentDataArray0.Length, innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer<T>
            where T : struct, IBaseJobProcessComponentData_2
        {
            static IntPtr s_JobReflectionData;
            static ComponentType s_Type0;
            static ComponentType s_Type1;
            
            
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray1;
            public T                                       Data;
                        
            public static IntPtr Initialize(out ComponentType type0, out ComponentType type1)
            {
                if (s_JobReflectionData == IntPtr.Zero)
                {
                    foreach (var iType in typeof(T).GetInterfaces())
                    {
                        if (iType.Name.StartsWith("IJobProcessComponentData"))
                        {
                            var genericArgs = iType.GetGenericArguments();                            
                            var jobStructType = typeof(JobStruct_Process2<,,>).MakeGenericType(typeof(T), genericArgs[0], genericArgs[1]);
                            
                            var reflectionDataRes = jobStructType.GetMethod("Initialize").Invoke(null, null);

                            s_JobReflectionData = (IntPtr)reflectionDataRes;
                            Assert.IsTrue(s_JobReflectionData != IntPtr.Zero);

                            var executeMethodParameters = typeof(T).GetMethod("Execute").GetParameters();
                            bool readonly0 = executeMethodParameters[0].GetCustomAttributes(typeof(ReadOnlyAttribute)).Count() == 0;
                            bool readonly1 = executeMethodParameters[1].GetCustomAttributes(typeof(ReadOnlyAttribute)).Count() == 0;
                            type0 = s_Type0 = new ComponentType(genericArgs[0], readonly0 ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite);
                            type1 = s_Type1 = new ComponentType(genericArgs[1], readonly1 ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite);

                            return s_JobReflectionData;
                        }
                    }
                }

                type0 = s_Type0;
                type1 = s_Type1;
                return s_JobReflectionData;
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process2<T, U0, U1>
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            static IntPtr s_JobReflectionData;

            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U1>  ComponentDataArray1;
            public T                       Data;

            public static IntPtr Initialize()
            {
                if (s_JobReflectionData == IntPtr.Zero)
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process2<T, U0, U1>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);

                return s_JobReflectionData;
            }

            delegate void ExecuteJobFunction(ref JobStruct_Process2<T, U0, U1> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct_Process2<T, U0, U1> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
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
        struct DataGroup
        {
            internal ComponentDataArray<TComponentData0> Component0;
        }

        [Inject]
        DataGroup m_Group;

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
        struct DataGroup
        {
            internal ComponentDataArray<TComponentData0> Component0;
            internal ComponentDataArray<TComponentData1> Component1;
        }

        [Inject]
        DataGroup m_Group;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            const int batchSize = 32;

            var jobData = default(TJob);
            jobData.Prepare();

            return jobData.Schedule(m_Group.Component0, m_Group.Component1, batchSize, inputDeps);
        }
    }
}
