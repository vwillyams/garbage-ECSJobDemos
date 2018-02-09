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
            public AtomicSafetyHandle safetyHandle;
#endif
            public JobHandle writeFence;
            public int numReadFences;
        }

        const int kMaxReadJobHandles = 17;
        const int kMaxTypes = TypeManager.MaximumTypesCount;

        bool m_HasCleanHandles;
        bool m_IsInTransaction;

        JobHandle* m_ReadJobFences;
        ComponentSafetyHandle* m_ComponentSafetyHandles;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_TempSafety;
        AtomicSafetyHandle m_ExclusiveTransactionSafety;
#endif

        JobHandle m_ExclusiveTransactionDependency;

        JobHandle* m_JobDependencyCombineBuffer;
        int m_JobDependencyCombineBufferCount;

        //@TODO: Check against too many types created...

        public ComponentJobSafetyManager()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_TempSafety = AtomicSafetyHandle.Create();
#endif


            m_ReadJobFences = (JobHandle*) UnsafeUtility.Malloc(sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes, 16,
                Allocator.Persistent);
            UnsafeUtility.MemClear(m_ReadJobFences, sizeof(JobHandle) * kMaxReadJobHandles * kMaxTypes);

            m_ComponentSafetyHandles =
                (ComponentSafetyHandle*) UnsafeUtility.Malloc(sizeof(ComponentSafetyHandle) * kMaxTypes, 16,
                    Allocator.Persistent);
            UnsafeUtility.MemClear(m_ComponentSafetyHandles, sizeof(ComponentSafetyHandle) * kMaxTypes);

            m_JobDependencyCombineBufferCount = 4 * 1024;
            m_JobDependencyCombineBuffer = (JobHandle*) UnsafeUtility.Malloc(
                sizeof(ComponentSafetyHandle) * m_JobDependencyCombineBufferCount, 16, Allocator.Persistent);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i != kMaxTypes; i++)
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
            for (int i = 0; i < kMaxTypes; i++)
                m_ComponentSafetyHandles[i].writeFence.Complete();

            for (int i = 0; i < kMaxTypes * kMaxReadJobHandles; i++)
                m_ReadJobFences[i].Complete();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i < kMaxTypes; i++)
            {
                var res = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_ComponentSafetyHandles[i]
                    .safetyHandle);
                if (res == EnforceJobResult.DidSyncRunningJobs)
                {
                    //@TODO: EnforceAllBufferJobsHaveCompletedAndRelease should probably print the error message and locate the exact job...
                    Debug.LogError(
                        "Disposing EntityManager but a job is still running against the ComponentData. It appears the job has not been registered with JobComponentSystem.AddDependency.");
                }
            }

            AtomicSafetyHandle.Release(m_TempSafety);

#endif

            UnsafeUtility.Free(m_JobDependencyCombineBuffer, Allocator.Persistent);

            UnsafeUtility.Free(m_ComponentSafetyHandles, Allocator.Persistent);
            m_ComponentSafetyHandles = null;

            UnsafeUtility.Free(m_ReadJobFences, Allocator.Persistent);
            m_ReadJobFences = null;
        }

        public void CompleteDependencies(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
            for (int i = 0; i != writerTypesCount; i++)
                CompleteReadAndWriteDependency(writerTypes[i]);

            for (int i = 0; i != readerTypesCount; i++)
                CompleteWriteDependency(readerTypes[i]);
        }

        public JobHandle GetDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (readerTypesCount * kMaxReadJobHandles + writerTypesCount > m_JobDependencyCombineBufferCount)
                throw new System.ArgumentException("Too many readers & writers in GetDependency");
#endif

            int count = 0;
            for (int i = 0; i != readerTypesCount; i++)
            {
                m_JobDependencyCombineBuffer[count++] = m_ComponentSafetyHandles[readerTypes[i]].writeFence;
            }

            for (int i = 0; i != writerTypesCount; i++)
            {
                int writerType = writerTypes[i];

                m_JobDependencyCombineBuffer[count++] = m_ComponentSafetyHandles[writerType].writeFence;

                int numReadFences = m_ComponentSafetyHandles[writerType].numReadFences;
                for (int j = 0; j != numReadFences; j++)
                    m_JobDependencyCombineBuffer[count++] = m_ReadJobFences[writerType * kMaxReadJobHandles + j];
            }

            return Unity.Jobs.LowLevel.Unsafe.JobHandleUnsafeUtility.CombineDependencies(m_JobDependencyCombineBuffer,
                count);
        }

        public void AddDependency(int* readerTypes, int readerTypesCount, int* writerTypes, int writerTypesCount,
            JobHandle dependency)
        {
            for (int i = 0; i != writerTypesCount; i++)
            {
                int writer = writerTypes[i];
                m_ComponentSafetyHandles[writer].writeFence = dependency;
            }

            for (int i = 0; i != readerTypesCount; i++)
            {
                int reader = readerTypes[i];
                m_ReadJobFences[reader * kMaxReadJobHandles + m_ComponentSafetyHandles[reader].numReadFences] =
                    dependency;
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
        public AtomicSafetyHandle GetSafetyHandle(int type, bool isReadOnly)
        {
            m_HasCleanHandles = false;
            AtomicSafetyHandle handle = m_ComponentSafetyHandles[type].safetyHandle;
            if (isReadOnly)
                AtomicSafetyHandle.UseSecondaryVersion(ref handle);

            return handle;
        }
#endif

        void CombineReadDependencies(int type)
        {
            var combined = Unity.Jobs.LowLevel.Unsafe.JobHandleUnsafeUtility.CombineDependencies(
                m_ReadJobFences + type * kMaxReadJobHandles, m_ComponentSafetyHandles[type].numReadFences);

            m_ReadJobFences[type * kMaxReadJobHandles] = combined;
            m_ComponentSafetyHandles[type].numReadFences = 1;
        }

        public bool IsInTransaction
        {
            get { return m_IsInTransaction; }
        }

        public JobHandle ExclusiveTransactionDependency
        {
            get
            {
                return m_ExclusiveTransactionDependency;
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!m_IsInTransaction)
                    throw new System.InvalidOperationException("EntityManager.TransactionDependency can only after EntityManager.BeginTransaction has been called.");

                if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(m_ExclusiveTransactionDependency, value))
                    throw new System.InvalidOperationException("EntityManager.TransactionDependency must depend on the Entity Transaction job.");
#endif
                m_ExclusiveTransactionDependency = value;
            }
        }

        public AtomicSafetyHandle ExclusiveTransactionSafety
        {
            get { return m_ExclusiveTransactionSafety; }
        }

        public void BeginTransaction()
        {
            if (m_IsInTransaction)
                return;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i != TypeManager.GetTypeCount(); i++)
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_ComponentSafetyHandles[i].safetyHandle);
#endif

            m_IsInTransaction = true;
            m_ExclusiveTransactionSafety = AtomicSafetyHandle.Create();
            m_ExclusiveTransactionDependency = GetAllDependencies();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i != TypeManager.GetTypeCount(); i++)
                AtomicSafetyHandle.Release(m_ComponentSafetyHandles[i].safetyHandle);
#endif

        }

        public void EndTransaction()
        {
            if (!m_IsInTransaction)
                return;
            
            m_ExclusiveTransactionDependency.Complete();

            var res = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_ExclusiveTransactionSafety);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (res != EnforceJobResult.AllJobsAlreadySynced)
                //@TODO: Better message
                Debug.LogError("EntityTransaction job has not been registered");                
#endif
            m_IsInTransaction = false;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i != TypeManager.GetTypeCount(); i++)
            {
                m_ComponentSafetyHandles[i].safetyHandle = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ComponentSafetyHandles[i].safetyHandle, false);
            }
#endif
        }

        JobHandle GetAllDependencies()
        {
            var jobHandles = new NativeArray<JobHandle>(TypeManager.GetTypeCount() * (kMaxReadJobHandles + 1), Allocator.Temp);
            
            int count = 0;
            for (int i = 0; i != TypeManager.GetTypeCount(); i++)
            {
                jobHandles[count++] = m_ComponentSafetyHandles[i].writeFence;

                int numReadFences = m_ComponentSafetyHandles[i].numReadFences;
                for (int j = 0; j != numReadFences; j++)
                    jobHandles[count++] = m_ReadJobFences[i * kMaxReadJobHandles + j];
            }

            var combined = JobHandle.CombineDependencies(jobHandles);
            jobHandles.Dispose();

            return combined;
        }
    }
}
