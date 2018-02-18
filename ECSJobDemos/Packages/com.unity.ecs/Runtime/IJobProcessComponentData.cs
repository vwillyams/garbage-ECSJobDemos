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

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IBaseJobProcessComponentData
    {
    }
    
    //@TODO: It would be nice to get rid of these interfaces completely.
    //Right now implementation needs it, but they pollute public API in annoying ways.
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IBaseJobProcessComponentData_3 : IBaseJobProcessComponentData
    {
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IBaseJobProcessComponentData_2 : IBaseJobProcessComponentData
    {
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IBaseJobProcessComponentData_1 : IBaseJobProcessComponentData
    {
    }

    public interface IJobProcessComponentData<T0> : IBaseJobProcessComponentData_1
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

    public interface IJobProcessComponentData<T0, T1, T2> : IBaseJobProcessComponentData_3
        where T0 : struct, IComponentData
        where T1 : struct, IComponentData
        where T2 : struct, IComponentData
    {
        void Execute(ref T0 data0, ref T1 data1, ref T2 data2);
    }
    
    struct JobProcessComponentDataCache
    {
        public IntPtr              JobReflectionData;
        public ComponentType[]     Types;
            
        public ComponentGroup      ComponentGroup;
        public ComponentSystemBase ComponentSystem;
    }
        
    static class IJobProcessComponentDataUtility
    {
        public static ComponentType[] GetComponentTypes(Type jobType)
        {
            var interfaceType = GetIJobProcessComponentDataInterface(jobType);
            if (interfaceType != null)
                return GetComponentTypes(jobType, interfaceType);
            else
                return null;
        }

        static ComponentType[] GetComponentTypes(Type jobType, Type interfaceType)
        {
            var genericArgs = interfaceType.GetGenericArguments();
        
            //@TODO: Readonly support
            //var executeMethodParameters = jobType.GetMethod("Execute").GetParameters();

            var componentTypes = new List<ComponentType>();
            
            for (int i = 0; i < genericArgs.Length; i++)
            {
                //bool isReadonly = executeMethodParameters[i].GetCustomAttributes(typeof(ReadOnlyAttribute)).Count() != 0 || executeMethodParameters[i].GetCustomAttributes(typeof(IsReadOnlyAttribute)).Count() != 0;
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

            return componentTypes.ToArray();
        }

        static IntPtr GetJobReflection(Type jobType, Type wrapperJobType, Type interfaceType)
        {
            var genericArgs = interfaceType.GetGenericArguments();
            
            var jobTypeAndGenericArgs = new List<Type>();
            jobTypeAndGenericArgs.Add(jobType);
            jobTypeAndGenericArgs.AddRange(genericArgs);
            var resolvedWrapperJobType = wrapperJobType.MakeGenericType(jobTypeAndGenericArgs.ToArray());
                    
            var reflectionDataRes = resolvedWrapperJobType.GetMethod("Initialize").Invoke(null, null);
            return (IntPtr)reflectionDataRes;  
        }

        static Type GetIJobProcessComponentDataInterface(Type jobType)
        {
            foreach (var iType in jobType.GetInterfaces())
            {
                if (iType.Assembly == typeof(IBaseJobProcessComponentData).Assembly && iType.Name.StartsWith("IJobProcessComponentData"))
                    return iType;
            }

            return null;
        }
        
        internal static void Initialize(ComponentSystemBase system, Type jobType, Type wrapperJobType, ref JobProcessComponentDataCache cache)
        {
            if (cache.JobReflectionData == IntPtr.Zero)
            {
                var iType = GetIJobProcessComponentDataInterface(jobType);
                cache.JobReflectionData = GetJobReflection(jobType, wrapperJobType, iType);  
                cache.Types = GetComponentTypes(jobType, iType);
                
                Assert.AreNotEqual(null, wrapperJobType );
                Assert.AreNotEqual(null, iType);
            }
            
            if (cache.ComponentSystem != system)
            {
                cache.ComponentGroup = system.GetComponentGroup(cache.Types);
                cache.ComponentSystem = system;
            }
        }
    }

    public static class JobProcessComponentDataExtensions
    {
        //NOTE: It would be much better if C# could resolve the branch with generic resolving,
        //      but apparently the interface constraint is not enough..
        
        public static JobHandle Schedule<T>(this T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn = default(JobHandle))
            where T : struct, IBaseJobProcessComponentData
        {
            var typeT = typeof(T);
            if (typeof(IBaseJobProcessComponentData_1).IsAssignableFrom(typeT))
                return ScheduleInternal_1(ref jobData, system, innerloopBatchCount, dependsOn, ScheduleMode.Batched);
            else if (typeof(IBaseJobProcessComponentData_2).IsAssignableFrom(typeT))
                return ScheduleInternal_2(ref jobData, system, innerloopBatchCount, dependsOn, ScheduleMode.Batched);
            else
                return ScheduleInternal_3(ref jobData, system, innerloopBatchCount, dependsOn, ScheduleMode.Batched);
        }

        public static void Run<T>(this T jobData, ComponentSystemBase system)
            where T : struct, IBaseJobProcessComponentData
        {
            var typeT = typeof(T);
            if (typeof(IBaseJobProcessComponentData_1).IsAssignableFrom(typeT ))
                ScheduleInternal_1(ref jobData, system, -1, default(JobHandle), ScheduleMode.Run);
            else if (typeof(IBaseJobProcessComponentData_2).IsAssignableFrom(typeT))
                ScheduleInternal_2(ref jobData, system, -1, default(JobHandle), ScheduleMode.Run);
            else
                ScheduleInternal_3(ref jobData, system, -1, default(JobHandle), ScheduleMode.Run);
        }

        internal unsafe static JobHandle ScheduleInternal_1<T>(ref T jobData, ComponentSystemBase system, int innerloopBatchCount,
            JobHandle dependsOn, ScheduleMode mode)
            where T : struct
        {
            IJobProcessComponentDataUtility.Initialize(system, typeof(T), typeof(JobStruct_Process1<,>), ref JobStruct_ProcessInfer_1<T>.Cache);

            var group = JobStruct_ProcessInfer_1<T>.Cache.ComponentGroup;
            var types = JobStruct_ProcessInfer_1<T>.Cache.Types;

            JobStruct_ProcessInfer_1<T> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = group.GetComponentDataArray<ProxyComponentData>(types[0]);

            var length = fullData.ComponentDataArray0.Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_1<T>.Cache.JobReflectionData, dependsOn, mode);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_1<T> where T : struct
        {
            public static JobProcessComponentDataCache Cache;

            [NativeMatchesParallelForLength] 
            public ComponentDataArray<ProxyComponentData> ComponentDataArray0;
            public T Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process1<T, U0>
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            [NativeMatchesParallelForLength] 
            public ComponentDataArray<U0> ComponentDataArray;
            public T Data;

            public static IntPtr Initialize()
            {
                return JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process1<T, U0>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction) Execute);
            }

            delegate void ExecuteJobFunction(ref JobStruct_Process1<T, U0> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct_Process1<T, U0> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
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

        
        internal unsafe static JobHandle ScheduleInternal_2<T>(ref T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn, ScheduleMode mode)
            where T : struct
        {
            IJobProcessComponentDataUtility.Initialize(system, typeof(T), typeof(JobStruct_Process2<,,>), ref JobStruct_ProcessInfer_2<T>.Cache);

            var group = JobStruct_ProcessInfer_2<T>.Cache.ComponentGroup;
            var types = JobStruct_ProcessInfer_2<T>.Cache.Types;
            
            JobStruct_ProcessInfer_2<T> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = group.GetComponentDataArray<ProxyComponentData>(types[0]);
            fullData.ComponentDataArray1 = group.GetComponentDataArray<ProxyComponentData>(types[1]);

            var length = fullData.ComponentDataArray0.Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_2<T>.Cache.JobReflectionData, dependsOn, mode);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_2<T> where T : struct
        {
            public static JobProcessComponentDataCache Cache;
            
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
        
        internal unsafe static JobHandle ScheduleInternal_3<T>(ref T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn, ScheduleMode mode)
            where T : struct
        {
            IJobProcessComponentDataUtility.Initialize(system, typeof(T), typeof(JobStruct_Process3<,,,>), ref JobStruct_ProcessInfer_3<T>.Cache);

            var group = JobStruct_ProcessInfer_3<T>.Cache.ComponentGroup;
            var types = JobStruct_ProcessInfer_3<T>.Cache.Types;
            
            JobStruct_ProcessInfer_3<T> fullData;
            fullData.Data = jobData;
            fullData.ComponentDataArray0 = group.GetComponentDataArray<ProxyComponentData>(types[0]);
            fullData.ComponentDataArray1 = group.GetComponentDataArray<ProxyComponentData>(types[1]);
            fullData.ComponentDataArray2 = group.GetComponentDataArray<ProxyComponentData>(types[2]);

            var length = fullData.ComponentDataArray0.Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_3<T>.Cache.JobReflectionData, dependsOn, mode);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_3<T> where T : struct
        {
            public static JobProcessComponentDataCache Cache;
            
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray1;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<ProxyComponentData>  ComponentDataArray2;
            public T                                       Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process3<T, U0, U1, U2>
            where T : struct, IJobProcessComponentData<U0, U1, U2>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
            where U2 : struct, IComponentData
        {
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U0>  ComponentDataArray0;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U1>  ComponentDataArray1;
            [NativeMatchesParallelForLength]
            public ComponentDataArray<U2>  ComponentDataArray2;
            public T                       Data;

            public static IntPtr Initialize()
            {
                return JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process3<T, U0, U1, U2>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobStruct_Process3<T, U0, U1, U2> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct_Process3<T, U0, U1, U2> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        int len0, len1, len2;
                        var ptr0 = jobData.ComponentDataArray0.GetUnsafeChunkPtr(begin, end - begin, out len0);
                        var ptr1 = jobData.ComponentDataArray1.GetUnsafeChunkPtr(begin, end - begin, out len1);
                        var ptr2 = jobData.ComponentDataArray2.GetUnsafeChunkPtr(begin, end - begin, out len2);
                        //@TODO: Currently Assert.AreEqual doens't compile in burst. Need to find out why...
                        // Assert.AreEqual(array0.Length, array1.Length);

                        for (int i = 0; i != len0; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = UnsafeUtility.ReadArrayElement<U0>(ptr0, i);
                            var value1 = UnsafeUtility.ReadArrayElement<U1>(ptr1, i);
                            var value2 = UnsafeUtility.ReadArrayElement<U2>(ptr2, i);

                            jobData.Data.Execute(ref value0, ref value1, ref value2);

                            UnsafeUtility.WriteArrayElement(ptr0, i, value0);
                            UnsafeUtility.WriteArrayElement(ptr1, i, value1);
                            UnsafeUtility.WriteArrayElement(ptr2, i, value2);
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
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            const int batchSize = 32;

            var jobData = default(TJob);
            jobData.Prepare();

            return JobProcessComponentDataExtensions.ScheduleInternal_1(ref jobData, this, batchSize, inputDeps, ScheduleMode.Batched);
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

            return JobProcessComponentDataExtensions.ScheduleInternal_2(ref jobData, this, batchSize, inputDeps, ScheduleMode.Batched);
        }
    }
}
