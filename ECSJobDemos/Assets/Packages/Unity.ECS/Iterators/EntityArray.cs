﻿using System;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{

	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	public unsafe struct EntityArray
	{
		ComponentChunkIterator 		m_Iterator;
		ComponentChunkCache 		m_Cache;
		int                     	m_Length;

		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		int                      	m_MinIndex;
		int                      	m_MaxIndex;
		AtomicSafetyHandle       	m_Safety;
		#endif

		#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal unsafe EntityArray(ComponentChunkIterator iterator, int length, AtomicSafetyHandle safety)
		#else
        internal unsafe EntityArray(ComponentChunkIterator iterator, int length)
		#endif
		{
            m_Length = length;
            m_Iterator = iterator;
			m_Cache = default(ComponentChunkCache);

			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_MinIndex = 0;
			m_MaxIndex = length - 1;
			m_Safety = safety;
			#endif

		}

		public unsafe Entity this[int index]
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

                return UnsafeUtility.ReadArrayElement<Entity>(m_Cache.CachedPtr, index);
			}
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        void FailOutOfRangeError(int index)
		{
			//@TODO: Make error message utility and share with NativeArray...
			if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
				throw new IndexOutOfRangeException(string.Format("Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\nReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.", index, m_MinIndex, m_MaxIndex));

			throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
		}
#endif

        public int Length { get { return m_Length; } }
	}
}