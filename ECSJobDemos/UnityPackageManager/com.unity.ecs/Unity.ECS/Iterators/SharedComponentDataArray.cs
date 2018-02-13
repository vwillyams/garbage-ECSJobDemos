using Unity.Collections.LowLevel.Unsafe;
using System;
using Unity.ECS;

namespace UnityEngine.ECS
{

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public struct SharedComponentDataArray<T> where T : struct, ISharedComponentData
    {
        private ComponentChunkIterator m_Iterator;
        private ComponentChunkCache    m_Cache;
        private SharedComponentDataManager m_sharedComponentDataManager;
        private int m_sharedComponentIndex;

        private int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private int m_MinIndex;
        private int m_MaxIndex;
        private AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal SharedComponentDataArray(SharedComponentDataManager sharedComponentDataManager, int sharedComponentIndex, ComponentChunkIterator iterator, int length, AtomicSafetyHandle safety)
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

        public T this[int index]
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

                var sharedComponent = m_Iterator.GetSharedComponentFromCurrentChunk(m_sharedComponentIndex);
                return m_sharedComponentDataManager.GetSharedComponentData<T>(sharedComponent);
            }
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void FailOutOfRangeError(int index)
		{
			//@TODO: Make error message utility and share with NativeArray...
			if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
				throw new IndexOutOfRangeException(
				        $"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.\n" +
				        "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
				        "You can use double buffering strategies to avoid race conditions due to " +
				        "reading & writing in parallel to the same elements from a job.");

			throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
		}
#endif

        public int Length => m_Length;
    }
}
