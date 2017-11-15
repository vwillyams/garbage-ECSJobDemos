using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Collections;

namespace UnityEngine.ECS
{
    unsafe class ComponentJobSafetyManager
    {
        public unsafe struct ComponentSafetyHandle
        {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            public AtomicSafetyHandle       safetyHandle;
    #endif
            public JobHandle                writeFence;
            public int                      numReadFences;
        }

        const int kMaxReadJobHandles = 17;
        const int kMaxTypes = 5000;

        bool                    m_HasCleanHandles;

        JobHandle*              m_ReadJobFences;
        ComponentSafetyHandle*  m_ComponentSafetyHandles;
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle      m_TempSafety;
        #endif
        
        NativeList<JobHandle>   m_JobDependencyCombineList;


        //@TODO: Check against too many types created...

        public ComponentJobSafetyManager()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_TempSafety = AtomicSafetyHandle.Create();
#endif
            m_ReadJobFences = (JobHandle*)UnsafeUtility.Malloc(sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear((IntPtr)m_ReadJobFences, sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes);

            m_ComponentSafetyHandles = (ComponentSafetyHandle*)UnsafeUtility.Malloc(sizeof(ComponentSafetyHandle) * kMaxTypes, 16, Allocator.Persistent);
            UnsafeUtility.MemClear((IntPtr)m_ComponentSafetyHandles, sizeof(ComponentSafetyHandle) * kMaxTypes);

            m_JobDependencyCombineList = new NativeList<JobHandle>(Allocator.Persistent);
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i != kMaxTypes;i++)
            {
                m_ComponentSafetyHandles[i].safetyHandle = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ComponentSafetyHandles[i].safetyHandle, false);
            }
#endif

            m_HasCleanHandles = true;
        }

        //@TODO: Optimize as one function call to in batch bump version on every single handle...
        public void CompleteAllJobsAndInvalidateArrays()
        {
            if (m_HasCleanHandles)
                return;

            Profiling.Profiler.BeginSample("CompleteAllJobsAndInvalidateArrays");

            int count = TypeManager.GetTypeCount();
            for (int t = 0; t != count; t++)
            {
                m_ComponentSafetyHandles[t].writeFence.Complete();

                int readFencesCount = m_ComponentSafetyHandles[t].numReadFences;
                JobHandle* readFences = m_ReadJobFences + t * kMaxReadJobHandles;
                for (int r = 0; r != readFencesCount; r++)
                    readFences[r].Complete();
                m_ComponentSafetyHandles[t].numReadFences = 0;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i != count; i++)
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_ComponentSafetyHandles[i].safetyHandle);

            for (int i = 0; i != count; i++)
            {
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].safetyHandle);
                m_ComponentSafetyHandles[i].safetyHandle = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ComponentSafetyHandles[i].safetyHandle, false);
            }
#endif

            m_HasCleanHandles = true;

            Profiling.Profiler.EndSample();
        }

        public void Dispose()
        {
            m_JobDependencyCombineList.Dispose();
            
            for (int i = 0; i < kMaxTypes;i++)
                m_ComponentSafetyHandles[i].writeFence.Complete();

            for (int i = 0; i < kMaxTypes * kMaxReadJobHandles; i++)
                m_ReadJobFences[i].Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i < kMaxTypes; i++)
            {
                var res = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_ComponentSafetyHandles[i].safetyHandle);
                if (res == EnforceJobResult.DidSyncRunningJobs)
                {
                    //@TODO: EnforceAllBufferJobsHaveCompletedAndRelease should probably print the error message and locate the exact job...
                    Debug.LogError("Disposing EntityManager but a job is still running against the ComponentData. It appears the job has not been registered with JobComponentSystem.AddDependency.");
                }
            }

            AtomicSafetyHandle.Release(m_TempSafety);
#endif
            UnsafeUtility.Free((IntPtr)m_ComponentSafetyHandles, Allocator.Persistent);
            m_ComponentSafetyHandles = null;

            UnsafeUtility.Free((IntPtr)m_ReadJobFences, Allocator.Persistent);
            m_ReadJobFences = null;
        }

        public void CompleteDependencies(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
            for (int i = 0; i != writerTypesCount; i++)
                CompleteReadAndWriteDependency(writerTypes[i]);

            for (int i = 0; i != readerTypesCount; i++)
                CompleteWriteDependency(readerTypes[i]);
        }

        public JobHandle GetDependency(int* writerTypes, int writerTypesCount, int* readerTypes, int readerTypesCount)
        {
            m_JobDependencyCombineList.Clear();
            
            for (int i = 0; i != writerTypesCount; i++)
                m_JobDependencyCombineList.Add(m_ComponentSafetyHandles[writerTypes[i]].writeFence);
            
            for (int i = 0; i != readerTypesCount; i++)
            {
                int readerType = readerTypes[i];
                m_JobDependencyCombineList.Add(m_ComponentSafetyHandles[readerType ].writeFence);
                
                int numReadFences = m_ComponentSafetyHandles[readerType ].numReadFences;
                for (int j = 0; j != numReadFences; j++)
                    m_JobDependencyCombineList.Add(m_ReadJobFences[readerType * kMaxReadJobHandles + j]);
            }
            
            return JobHandle.CombineDependencies(m_JobDependencyCombineList);
        }

        public void AddDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount, JobHandle dependency)
        {
            
            for (int i = 0; i != writerTypesCount; i++)
            {
                //@TODO: Check that it depends on all previous dependencies...
                int writer = writerTypes[i];
                m_ComponentSafetyHandles[writer].writeFence = dependency;
            }

            for (int i = 0; i != readerTypesCount; i++)
            {
                int reader = readerTypes[i];
                m_ReadJobFences[reader  * kMaxReadJobHandles + m_ComponentSafetyHandles[reader ].numReadFences] = dependency;
                m_ComponentSafetyHandles[reader].numReadFences++;

                if (m_ComponentSafetyHandles[reader].numReadFences == kMaxReadJobHandles)
                    CombineReadDependencies(reader);
            }
            
            if (readerTypesCount != 0 || writerTypesCount != 0)
                m_HasCleanHandles = false;
        }

        public void CompleteWriteDependency(int type)
        {
            m_ComponentSafetyHandles[type].writeFence.Complete();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_ComponentSafetyHandles[type].safetyHandle);
#endif
        }

        public void CompleteReadAndWriteDependency(int type)
        {
            for (int i = 0; i < m_ComponentSafetyHandles[type].numReadFences; ++i)
                m_ReadJobFences[type * kMaxReadJobHandles + i].Complete();
            m_ComponentSafetyHandles[type].numReadFences = 0;

            m_ComponentSafetyHandles[type].writeFence.Complete();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_ComponentSafetyHandles[type].safetyHandle);
#endif
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public AtomicSafetyHandle GetSafetyHandle(int type)
        {
            m_HasCleanHandles = false;
            return m_ComponentSafetyHandles[type].safetyHandle;
        }
#endif

        void CombineReadDependencies(int type)
        {
            //@TODO: blah...
            var readFencesSlice = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<JobHandle>((IntPtr)(m_ReadJobFences + type * kMaxReadJobHandles), m_ComponentSafetyHandles[type].numReadFences, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref readFencesSlice, m_TempSafety);
#endif
            m_ReadJobFences[type * kMaxReadJobHandles] = JobHandle.CombineDependencies(readFencesSlice);
            m_ComponentSafetyHandles[type].numReadFences = 1;
        }
    }
}
