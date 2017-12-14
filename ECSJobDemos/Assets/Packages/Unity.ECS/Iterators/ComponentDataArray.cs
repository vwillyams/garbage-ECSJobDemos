﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace UnityEngine.ECS
{

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct ComponentDataArray<T> where T : struct, IComponentData
    {
        ComponentChunkIterator m_Iterator;
	    ComponentChunkCache    m_Cache;

        int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        int m_MinIndex;
        int m_MaxIndex;
        AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal unsafe ComponentDataArray(ComponentChunkIterator iterator, int length, AtomicSafetyHandle safety)
#else
        internal unsafe ComponentDataArray(ComponentChunkIterator iterator, int length)
#endif
        {
            m_Iterator = iterator;
	        m_Cache = default(ComponentChunkCache);
	        
            m_Length = length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Safety = safety;
#endif
        }

        public NativeArray<T> GetChunkArray(int startIndex, int maxCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

	        if (startIndex < m_MinIndex)
		        FailOutOfRangeError(startIndex);
	        else if (startIndex + maxCount > m_MaxIndex + 1)
		        FailOutOfRangeError(startIndex + maxCount);
#endif
            
            m_Iterator.UpdateCache(startIndex, out m_Cache);
            
            IntPtr ptr = m_Cache.CachedPtr + startIndex * m_Cache.CachedSizeOf;
            int count = Math.Min(maxCount, m_Cache.CachedEndIndex - startIndex);

	        var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, count, Allocator.Invalid);
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, m_Safety);
#endif

            return arr;
        }

        public void CopyTo(NativeSlice<T> dst, int startIndex = 0)
        {
            int copiedCount = 0;
            while (copiedCount < dst.Length)
            {
                var chunkArray = GetChunkArray(startIndex + copiedCount, dst.Length - copiedCount);
                dst.Slice(copiedCount, chunkArray.Length).CopyFrom(chunkArray);

                copiedCount += chunkArray.Length;
            }
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

                return UnsafeUtility.ReadArrayElement<T>(m_Cache.CachedPtr, index);
            }

			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
#endif

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
                    m_Iterator.UpdateCache(index, out m_Cache);

				UnsafeUtility.WriteArrayElement(m_Cache.CachedPtr, index, value);
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