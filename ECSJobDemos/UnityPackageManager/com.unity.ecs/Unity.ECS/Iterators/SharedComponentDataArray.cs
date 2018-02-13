using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using Unity.ECS;

namespace UnityEngine.ECS
{

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct SharedComponentDataArray<T> where T : struct, ISharedComponentData
    {
        ComponentChunkIterator m_Iterator;
	    ComponentChunkCache    m_Cache;
        SharedComponentDataManager m_sharedComponentDataManager;
        int m_sharedComponentIndex;

        int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        int m_MinIndex;
        int m_MaxIndex;
        AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal unsafe SharedComponentDataArray(SharedComponentDataManager sharedComponentDataManager, int sharedComponentIndex, ComponentChunkIterator iterator, int length, AtomicSafetyHandle safety)
#else
        internal unsafe SharedComponentDataArray(SharedComponentDataManager sharedComponentDataManager, int sharedComponentIndex, ComponentChunkIterator iterator, int length)
#endif
        {
            m_sharedComponentDataManager = sharedComponentDataManager;
            m_sharedComponentIndex = sharedComponentIndex;
            m_Iterator = iterator;
	        m_Cache = default(ComponentChunkCache);

            m_Length = length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Safety = safety;
#endif
        }

        public unsafe T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if (index < m_MinIndex || index > m_MaxIndex)
                    FailOutOfRangeError(index);
#endif

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
                    m_Iterator.UpdateCache(index, out m_Cache);

                int sharedComponent = m_Iterator.GetSharedComponentFromCurrentChunk(m_sharedComponentIndex);
                return m_sharedComponentDataManager.GetSharedComponentData<T>(sharedComponent);
            }
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        void FailOutOfRangeError(int index)
		{
			//@TODO: Make error message utility and share with NativeArray...
			if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
				throw new IndexOutOfRangeException(string.Format(
					"Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n" +
					"ReadWriteBuffers are restricted to only read & write the element at the job index. " +
					"You can use double buffering strategies to avoid race conditions due to " +
					"reading & writing in parallel to the same elements from a job.",
					index, m_MinIndex, m_MaxIndex));

			throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
		}
#endif

        public int Length { get { return m_Length; } }
	}
}
