using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
    public unsafe class ComponentJobSafetyManager
    {
        public unsafe struct ComponentSafetyHandle
        {
    #if ENABLE_NATIVE_ARRAY_CHECKS
            public AtomicSafetyHandle       safetyHandle;
    #endif
            public JobHandle                writeFence;
            public int                      numReadFences;
        }

        const int kMaxReadJobHandles = 17;
        const int kMaxTypes = 5000;

        JobHandle*              m_ReadJobFences;
        ComponentSafetyHandle*  m_ComponentSafetyHandles;
        AtomicSafetyHandle      m_TempSafety;

        //@TODO: Check agaisnt too many types created...

        public ComponentJobSafetyManager()
        {
            m_TempSafety = AtomicSafetyHandle.Create();
            m_ReadJobFences = (JobHandle*)UnsafeUtility.Malloc(sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear((IntPtr)m_ReadJobFences, sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes);

            m_ComponentSafetyHandles = (ComponentSafetyHandle*)UnsafeUtility.Malloc(sizeof(ComponentSafetyHandle) * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear((IntPtr)m_ComponentSafetyHandles, sizeof(ComponentSafetyHandle) * kMaxTypes);

            for (int i = 0; i != kMaxTypes;i++)
            {
                m_ComponentSafetyHandles[i].safetyHandle = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ComponentSafetyHandles[i].safetyHandle, false);
            }
        }

        //@TODO: Optimize as one function call to in batch bump version on every single handle...
        public void CompleteAllJobsAndInvalidateArrays()
        {
            int count = RealTypeManager.GetTypeCount();
            for (int t = 0; t != count; t++)
            {
                m_ComponentSafetyHandles[t].writeFence.Complete();

                int readFencesCount = m_ComponentSafetyHandles[t].numReadFences;
                JobHandle* readFences = m_ReadJobFences + t * kMaxReadJobHandles;
                for (int r = 0; r != readFencesCount; r++)
                    readFences[r].Complete();
                m_ComponentSafetyHandles[t].numReadFences = 0;
            }

            for (int i = 0; i != count; i++)
            {
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].safetyHandle);
                m_ComponentSafetyHandles[i].safetyHandle = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ComponentSafetyHandles[i].safetyHandle, false);
            }
        }


        public void CompleteJobsForType(int* types, int typeCount)
        {
            for (int i = 0; i != typeCount; i++)
            {
                int type = types[i];
                m_ComponentSafetyHandles[type].writeFence.Complete();

                int readFencesCount = m_ComponentSafetyHandles[type].numReadFences;
                JobHandle* readFences = m_ReadJobFences + type * kMaxReadJobHandles;
                for (int r = 0; r != readFencesCount; r++)
                    readFences[r].Complete();
                m_ComponentSafetyHandles[type].numReadFences = 0;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < kMaxTypes;i++)
            {
    #if ENABLE_NATIVE_ARRAY_CHECKS
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].safetyHandle);
    #endif
                m_ComponentSafetyHandles[i].writeFence.Complete();
            }

            for (int i = 0; i < kMaxTypes * kMaxReadJobHandles; i++)
            {
                m_ReadJobFences[i].Complete();
            }

            UnsafeUtility.Free((IntPtr)m_ReadJobFences, Allocator.Persistent);
            m_ReadJobFences = null;

            UnsafeUtility.Free((IntPtr)m_ComponentSafetyHandles, Allocator.Persistent);
            m_ComponentSafetyHandles = null;
        }

        public void CompleteWriteDependency(int type)
        {
            m_ComponentSafetyHandles[type].writeFence.Complete();
        }

        public void CompleteReadDependency(int type)
        {
            for (int i = 0; i < m_ComponentSafetyHandles[type].numReadFences; ++i)
                m_ReadJobFences[type * kMaxReadJobHandles + i].Complete();
            m_ComponentSafetyHandles[type].numReadFences = 0;

        }

        public JobHandle GetWriteDependency(int type)
        {
            return m_ComponentSafetyHandles[type].writeFence;
        }

        public JobHandle GetReadDependency(int type)
        {
            int numReadFences = m_ComponentSafetyHandles[type].numReadFences;
            if (numReadFences == 0)
                return new JobHandle();

            if (numReadFences > 1)
                CombineReadDependencies(type);

            return m_ReadJobFences[type * kMaxReadJobHandles + 0];
        }

        public void AddWriteDependency(int type, JobHandle fence)
        {
            //@TODO: Check that it depends on all previous dependencies...
            m_ComponentSafetyHandles[type].writeFence = fence;
        }

        public void AddReadDependency(int type, JobHandle fence)
        {
            m_ReadJobFences[type * kMaxReadJobHandles + m_ComponentSafetyHandles[type].numReadFences] = fence;
            m_ComponentSafetyHandles[type].numReadFences++;

            if (m_ComponentSafetyHandles[type].numReadFences == kMaxReadJobHandles)
                CombineReadDependencies(type);
        }

        public AtomicSafetyHandle GetSafetyHandle(int type)
        {
            return m_ComponentSafetyHandles[type].safetyHandle;
        }

        void CombineReadDependencies(int type)
        {
            //@TODO: blah...
            var readFencesSlice = NativeArray<JobHandle>.ConvertExistingDataToNativeArrayInternal((IntPtr)(m_ReadJobFences + type * kMaxReadJobHandles), m_ComponentSafetyHandles[type].numReadFences, m_TempSafety, Allocator.Invalid);
            m_ReadJobFences[type * kMaxReadJobHandles] = JobHandle.CombineDependencies(readFencesSlice);
            m_ComponentSafetyHandles[type].numReadFences = 1;
        }


}
}
