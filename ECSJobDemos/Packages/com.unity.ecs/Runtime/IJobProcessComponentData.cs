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

        public int                 ProcessTypesCount;
            
        public ComponentGroup      ComponentGroup;
        public ComponentSystemBase ComponentSystem;
    }
        
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    
    [StructLayout(LayoutKind.Sequential)]
    struct ProcessIterationData
    {
        public ComponentChunkIterator    Iterator0;
        public ComponentChunkIterator    Iterator1;
        public ComponentChunkIterator    Iterator2;
            
        public int                 IsReadOnly0;
        public int                 IsReadOnly1;
        public int                 IsReadOnly2;

        public int                 m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS

        public int m_MinIndex;
        public int m_MaxIndex;
            
#pragma warning disable 414
        public int                         m_SafetyReadOnlyCount;
        public int                         m_SafetyReadWriteCount;
        public AtomicSafetyHandle          m_Safety0;
        public AtomicSafetyHandle          m_Safety1;
        public AtomicSafetyHandle          m_Safety2;
#pragma warning restore
#endif
    }
    
    static class IJobProcessComponentDataUtility
    {
        public static ComponentType[] GetComponentTypes(Type jobType)
        {
            var interfaceType = GetIJobProcessComponentDataInterface(jobType);
            if (interfaceType != null)
            {
                int temp;
                return GetComponentTypes(jobType, interfaceType, out temp);
            }
            else
                return null;
        }

        static ComponentType[] GetComponentTypes(Type jobType, Type interfaceType, out int processCount)
        {
            var genericArgs = interfaceType.GetGenericArguments();
        
            var executeMethodParameters = jobType.GetMethod("Execute").GetParameters();

            var componentTypes = new List<ComponentType>();

            for (int i = 0; i < genericArgs.Length; i++)
            {
                bool isReadonly = executeMethodParameters[i].GetCustomAttributes(typeof(ReadOnlyAttribute)).Count() != 0 || executeMethodParameters[i].GetCustomAttributes(typeof(IsReadOnlyAttribute)).Count() != 0;
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

            processCount = genericArgs.Length;
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
        
        unsafe internal static void Initialize(ComponentSystemBase system, Type jobType, Type wrapperJobType, ref JobProcessComponentDataCache cache, out ProcessIterationData iterator)
        {
            if (cache.JobReflectionData == IntPtr.Zero)
            {
                var iType = GetIJobProcessComponentDataInterface(jobType);
                cache.JobReflectionData = GetJobReflection(jobType, wrapperJobType, iType);  
                cache.Types = GetComponentTypes(jobType, iType, out cache.ProcessTypesCount);

                Assert.AreNotEqual(null, wrapperJobType );
                Assert.AreNotEqual(null, iType);
            }
            
            if (cache.ComponentSystem != system)
            {
                cache.ComponentGroup = system.GetComponentGroup(cache.Types);
                cache.ComponentSystem = system;
            }

            var group = cache.ComponentGroup;
            
            // Readonly
            iterator.IsReadOnly0 = iterator.IsReadOnly1 = iterator.IsReadOnly2 = 0;
            fixed (int* isReadOnly = &iterator.IsReadOnly0)
            {
                for (int i = 0; i != cache.ProcessTypesCount; i++)
                    isReadOnly[i] = cache.Types[i].AccessModeType == ComponentType.AccessMode.ReadOnly ? 1 : 0; 
            }
            
            // Iterator & length
            iterator.Iterator0 = default(ComponentChunkIterator);
            iterator.Iterator1 = default(ComponentChunkIterator);
            iterator.Iterator2 = default(ComponentChunkIterator);
            int length = -1;
            fixed (ComponentChunkIterator* iterators = &iterator.Iterator0)
            {
                for (int i = 0; i != cache.ProcessTypesCount; i++)
                {
                    group.GetComponentChunkIterator(out length, out iterators[i]);
                    iterators[i].IndexInComponentGroup = group.GetIndexInComponentGroup(cache.Types[i].TypeIndex);
                }
            }
            
            iterator.m_Length = length;
            iterator.m_MaxIndex = length-1;
            iterator.m_MinIndex = 0;

            // Safety
            iterator.m_Safety0 = iterator.m_Safety1 = iterator.m_Safety2 = default(AtomicSafetyHandle);
            
            iterator.m_SafetyReadOnlyCount = 0;
            fixed (AtomicSafetyHandle* safety = &iterator.m_Safety0)
            {
                for (int i = 0; i != cache.ProcessTypesCount; i++)
                {
                    if (cache.Types[i].AccessModeType == ComponentType.AccessMode.ReadOnly)
                    {
                        safety[iterator.m_SafetyReadOnlyCount] = group.GetSafetyHandle(group.GetIndexInComponentGroup(cache.Types[i].TypeIndex));
                        iterator.m_SafetyReadOnlyCount++;
                    }
                }
            }

            iterator.m_SafetyReadWriteCount = 0;
            fixed (AtomicSafetyHandle* safety = &iterator.m_Safety0)
            {
                for (int i = 0; i != cache.ProcessTypesCount; i++)
                {
                    if (cache.Types[i].AccessModeType == ComponentType.AccessMode.ReadWrite)
                    {
                        safety[iterator.m_SafetyReadOnlyCount + iterator.m_SafetyReadWriteCount] = group.GetSafetyHandle(group.GetIndexInComponentGroup(cache.Types[i].TypeIndex));
                        iterator.m_SafetyReadWriteCount++;
                    }
                }
            }
            Assert.AreEqual(cache.ProcessTypesCount, iterator.m_SafetyReadWriteCount + iterator.m_SafetyReadOnlyCount);
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
            JobStruct_ProcessInfer_1<T> fullData;
            fullData.Data = jobData;

            IJobProcessComponentDataUtility.Initialize(system, typeof(T), typeof(JobStruct_Process1<,>), ref JobStruct_ProcessInfer_1<T>.Cache, out fullData.Iterator);

            int length = fullData.Iterator.m_Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_1<T>.Cache.JobReflectionData, dependsOn, mode);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_1<T> where T : struct
        {
            public static JobProcessComponentDataCache Cache;

            public ProcessIterationData      Iterator;
            public T Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process1<T, U0>
            where T : struct, IJobProcessComponentData<U0>
            where U0 : struct, IComponentData
        {
            public ProcessIterationData      Iterator;
            public T Data;

            public static IntPtr Initialize()
            {
                return JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process1<T, U0>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction) Execute);
            }

            delegate void ExecuteJobFunction(ref JobStruct_Process1<T, U0> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct_Process1<T, U0> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                ComponentChunkCache cache0;

                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        jobData.Iterator.Iterator0.UpdateCache(begin, out cache0);
                        var ptr0 = cache0.CachedPtr;

                        int curEnd = Math.Min(end, cache0.CachedEndIndex);
                    
                        for (int i = begin; i != curEnd; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = UnsafeUtility.ReadArrayElement<U0>(ptr0, i);

                            jobData.Data.Execute(ref value0);

                            if (jobData.Iterator.IsReadOnly0 == 0)
                                UnsafeUtility.WriteArrayElement(ptr0, i, value0);
                        }

                        begin = curEnd;
                    }
                }
            }
        }

        
        internal unsafe static JobHandle ScheduleInternal_2<T>(ref T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn, ScheduleMode mode)
            where T : struct
        {
            JobStruct_ProcessInfer_2<T> fullData;
            fullData.Data = jobData;

            IJobProcessComponentDataUtility.Initialize(system, typeof(T), typeof(JobStruct_Process2<,,>), ref JobStruct_ProcessInfer_2<T>.Cache, out fullData.Iterator);

            int length = fullData.Iterator.m_Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_2<T>.Cache.JobReflectionData, dependsOn, mode);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }



        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_2<T> where T : struct
        {
            public static JobProcessComponentDataCache Cache;

            public ProcessIterationData      Iterator;
            public T                         Data;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process2<T, U0, U1>
            where T : struct, IJobProcessComponentData<U0, U1>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
        {
            public ProcessIterationData      Iterator;
            public T                         Data;

            public static IntPtr Initialize()
            {
                return JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process2<T, U0, U1>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobStruct_Process2<T, U0, U1> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct_Process2<T, U0, U1> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                ComponentChunkCache cache0, cache1;
                
                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        jobData.Iterator.Iterator0.UpdateCache(begin, out cache0);
                        var ptr0 = cache0.CachedPtr;

                        jobData.Iterator.Iterator1.UpdateCache(begin, out cache1);
                        var ptr1 = cache1.CachedPtr;

                        int curEnd = Math.Min(end, cache0.CachedEndIndex);
                        
                        for (int i = begin; i != curEnd; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = UnsafeUtility.ReadArrayElement<U0>(ptr0, i);
                            var value1 = UnsafeUtility.ReadArrayElement<U1>(ptr1, i);

                            jobData.Data.Execute(ref value0, ref value1);

                            if (jobData.Iterator.IsReadOnly0 == 0)
                                UnsafeUtility.WriteArrayElement(ptr0, i, value0);
                            if (jobData.Iterator.IsReadOnly1 == 0)
                                UnsafeUtility.WriteArrayElement(ptr1, i, value1);
                        }

                        begin = curEnd;
                    }
                }
            }
        }
        
        internal unsafe static JobHandle ScheduleInternal_3<T>(ref T jobData, ComponentSystemBase system, int innerloopBatchCount, JobHandle dependsOn, ScheduleMode mode)
            where T : struct
        {
            JobStruct_ProcessInfer_3<T> fullData;
            fullData.Data = jobData;

            IJobProcessComponentDataUtility.Initialize(system, typeof(T), typeof(JobStruct_Process3<,,,>), ref JobStruct_ProcessInfer_3<T>.Cache, out fullData.Iterator);

            int length = fullData.Iterator.m_Length;
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData), JobStruct_ProcessInfer_3<T>.Cache.JobReflectionData, dependsOn, mode);
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, length, innerloopBatchCount <= 0 ? length : innerloopBatchCount);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_ProcessInfer_3<T> where T : struct
        {
            public static JobProcessComponentDataCache Cache;
            
            public ProcessIterationData      Iterator;
            public T                                       Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobStruct_Process3<T, U0, U1, U2>
            where T : struct, IJobProcessComponentData<U0, U1, U2>
            where U0 : struct, IComponentData
            where U1 : struct, IComponentData
            where U2 : struct, IComponentData
        {
            public ProcessIterationData      Iterator;
            public T                       Data;

            public static IntPtr Initialize()
            {
                return JobsUtility.CreateJobReflectionData(typeof(JobStruct_Process3<T, U0, U1, U2>), typeof(T), JobType.ParallelFor, (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobStruct_Process3<T, U0, U1, U2> data, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            static unsafe void Execute(ref JobStruct_Process3<T, U0, U1, U2> jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                ComponentChunkCache cache0, cache1, cache2;

                int begin;
                int end;
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                {
                    while (begin != end)
                    {
                        jobData.Iterator.Iterator0.UpdateCache(begin, out cache0);
                        var ptr0 = cache0.CachedPtr;

                        jobData.Iterator.Iterator1.UpdateCache(begin, out cache1);
                        var ptr1 = cache1.CachedPtr;

                        jobData.Iterator.Iterator2.UpdateCache(begin, out cache2);
                        var ptr2 = cache2.CachedPtr;

                        int curEnd = Math.Min(end, cache0.CachedEndIndex);
                        
                        for (int i = begin; i != curEnd; i++)
                        {
                            //@TODO: use ref returns to pass by ref instead of double copy
                            var value0 = UnsafeUtility.ReadArrayElement<U0>(ptr0, i);
                            var value1 = UnsafeUtility.ReadArrayElement<U1>(ptr1, i);
                            var value2 = UnsafeUtility.ReadArrayElement<U2>(ptr2, i);

                            jobData.Data.Execute(ref value0, ref value1, ref value2);

                            if (jobData.Iterator.IsReadOnly0 == 0)
                                UnsafeUtility.WriteArrayElement(ptr0, i, value0);
                            if (jobData.Iterator.IsReadOnly1 == 0)
                                UnsafeUtility.WriteArrayElement(ptr1, i, value1);
                            if (jobData.Iterator.IsReadOnly2 == 0)
                                UnsafeUtility.WriteArrayElement(ptr2, i, value2);
                        }

                        begin = curEnd;
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
