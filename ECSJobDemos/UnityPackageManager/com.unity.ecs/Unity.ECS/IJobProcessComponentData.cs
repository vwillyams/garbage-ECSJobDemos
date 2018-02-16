using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

namespace Unity.ECS
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class RequireComponentTagAttribute : System.Attribute
    {
        public Type[] TagComponents;

        public RequireComponentTagAttribute(params Type[] tagComponents)
        {
            TagComponents = tagComponents;
        }        
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public class RequireSubtractiveComponentAttribute : System.Attribute
    {
        public Type[] SubtractiveComponents;

        public RequireSubtractiveComponentAttribute(params Type[] subtractiveComponents)
        {
            SubtractiveComponents = subtractiveComponents;
        }
    }


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
        internal struct Cache
        {
            public IntPtr              JobReflectionData;
            public ComponentType[]     Types;
            
            public ComponentGroup      ComponentGroup;
            public ComponentSystemBase ComponentSystem;
        }
        
        internal static void Initialize(ComponentSystemBase system, Type jobType, Type wrapperJobType, ref Cache cache)
        {
            if (cache.JobReflectionData == IntPtr.Zero)
            {
                foreach (var iType in jobType.GetInterfaces())
                {
                    if (iType.Name.StartsWith("IJobProcessComponentData"))
                    {
                        var genericArgs = iType.GetGenericArguments() ;
                    
                        var jobTypeAndGenericArgs = new List<Type>();
                        jobTypeAndGenericArgs.Add(jobType);
                        jobTypeAndGenericArgs.AddRange(genericArgs);
                        var resolvedWrapperJobType = wrapperJobType.MakeGenericType(jobTypeAndGenericArgs.ToArray());
                            
                        var reflectionDataRes = resolvedWrapperJobType.GetMethod("Initialize").Invoke(null, null);
                        cache.JobReflectionData = (IntPtr)reflectionDataRes;  
                        //    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process2<T, U0, U1>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);

                        var executeMethodParameters = jobType.GetMethod("Execute").GetParameters();

                        var componentTypes = new List<ComponentType>();
                        
                        for (int i = 0; i < genericArgs.Length; i++)
                        {
                            //bool isReadonly = executeMethodParameters[i].GetCustomAttributes(typeof(ReadOnlyAttribute)).Count() != 0 || executeMethodParameters[i].GetCustomAttributes(typeof(IsReadOnlyAttribute)).Count() != 0;
                            //@TODO: Readonly not yet working...
                            bool isReadonly = false;
                            componentTypes.Add(new ComponentType(genericArgs[i], isReadonly ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite));
                        }

                        var subtractive = jobType.GetCustomAttribute<RequireSubtractiveComponentAttribute>();
                        if (subtractive != null)
                        {
                            foreach (var type in subtractive.SubtractiveComponents)
                                componentTypes.Add(ComponentType.Subtractive(type));
                        }

                        var requiredTags = jobType.GetCustomAttribute<RequireComponentTagAttribute>();
                        if (requiredTags != null)
                        {
                            //@TODO: Add Special component type which doesn't capture job dependencies... 
                            foreach (var type in requiredTags.TagComponents)
                                componentTypes.Add(ComponentType.ReadOnly(type));
                        }

                        cache.Types = componentTypes.ToArray();

                        break;
                    }
                }
            }
            
            if (cache.ComponentSystem != system)
            {
                cache.ComponentGroup = system.GetComponentGroup(cache.Types);
                cache.ComponentSystem = system;
            }
        }
        
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

        public static JobHandle Schedule<T>(this T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn = new JobHandle())
            where T : struct, IBaseJobProcessComponentData_2
        {
            return ScheduleInternal(ref jobData, system, innerloopBatchCount, dependsOn, ScheduleMode.Batched);
        }

        public static void Run<T>(this T jobData, ComponentSystemBase system, JobHandle dependsOn = new JobHandle())
            where T : struct, IBaseJobProcessComponentData_2
        {
            ScheduleInternal(ref jobData, system, -1, dependsOn, ScheduleMode.Run);
        }
        
        unsafe static JobHandle ScheduleInternal<T>(ref T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn, ScheduleMode mode)
            where T : struct, IBaseJobProcessComponentData_2
        {
            Initialize(system, typeof(T), typeof(JobStruct_Process2<,,>), ref JobStruct_ProcessInfer_2<T>.Cache);

            var group = JobStruct_ProcessInfer_2<T>.Cache.ComponentGroup;
            var types = JobStruct_ProcessInfer_2<T>.Cache.Types;
            
            JobStruct_ProcessInfer_2<T> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = group.GetComponentDataArray<ProxyComponentData>(types[0]);
            fullData.ComponentDataArray1 = group.GetComponentDataArray<ProxyComponentData>(types[1]);

            var length = fullData.ComponentDataArray0.Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_2<T>.Cache.JobReflectionData, dependsOn, ScheduleMode.Batched);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_2<T>
            where T : struct, IBaseJobProcessComponentData_2
        {
            public static Cache Cache;
            
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray1;
            public T                                       Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process2<T, U0, U1>
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U1>  ComponentDataArray1;
            public T                       Data;

            public static IntPtr Initialize()
            {
                return JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process2<T, U0, U1>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);
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
                        int len0, len1;
                        var ptr0 = jobData.ComponentDataArray0.GetUnsafeChunkPtr(begin, end - begin, out len0);
                        var ptr1 = jobData.ComponentDataArray1.GetUnsafeChunkPtr(begin, end - begin, out len1);
                        //@TODO: Currently Assert.AreEqual doens't compile in burst. Need to find out why...
                        // Assert.AreEqual(array0.Length, array1.Length);

                        for (int i = 0; i != len0; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = UnsafeUtility.ReadArrayElement<U0>(ptr0, i);
                            var value1 = UnsafeUtility.ReadArrayElement<U1>(ptr1, i);

                            jobData.Data.Execute(ref value0, ref value1);

                            UnsafeUtility.WriteArrayElement(ptr0, i, value0);
                            UnsafeUtility.WriteArrayElement(ptr1, i, value1);
                        }

                        begin += len0;
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
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            const int batchSize = 32;

            var jobData = default(TJob);
            jobData.Prepare();

            return jobData.Schedule(this, batchSize, inputDeps);
        }
    }
}
