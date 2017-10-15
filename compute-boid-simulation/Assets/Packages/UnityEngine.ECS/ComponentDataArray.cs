using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

//@TODO: ZERO TEST COVERAGE!!!
// * Doesn't handle all cases of how a NativeFreeList can be reallocate / invalidated etc

namespace UnityEngine.ECS
{

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct ComponentDataArray<T> where T : struct, IComponentData
    {
        ComponentDataArrayCache m_Cache;

#if ENABLE_NATIVE_ARRAY_CHECKS
        int m_Length;
        int m_MinIndex;
        int m_MaxIndex;
        AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_NATIVE_ARRAY_CHECKS
        public unsafe ComponentDataArray(ComponentDataArchetypeSegment* data, int length, AtomicSafetyHandle safety, bool isReadOnly)
#else
		public unsafe ComponentDataArray(ComponentDataArchetypeSegment* data, int length)
#endif
        {
            m_Cache = new ComponentDataArrayCache(data, length);

#if ENABLE_NATIVE_ARRAY_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Length = length;
            m_Safety = safety;
            if (isReadOnly)
                AtomicSafetyHandle.UseSecondaryVersion(ref m_Safety);
#endif

        }

        public void CopyTo(NativeSlice<T> dst, int startIndex = 0)
        {
            //@TODO Memcpy fast path if stride matches...
            for (int i = 0; i != dst.Length; i++)
            {
                dst[i] = this[i + startIndex];
            }
        }

        public unsafe T this[int index]
        {
            get
            {
#if ENABLE_NATIVE_ARRAY_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if (index < m_MinIndex || index > m_MaxIndex)
                    FailOutOfRangeError(index);
#else
				if ((uint)index >= (uint)m_Cache.m_Length)
					FailOutOfRangeError(index);
#endif

                if (index < m_Cache.m_CachedBeginIndex || index >= m_Cache.m_CachedEndIndex)
                    m_Cache.UpdateCache(index);

                return UnsafeUtility.ReadArrayElementWithStride<T>(m_Cache.m_CachedPtr, index, m_Cache.m_CachedStride);
            }

			set
			{
#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
#else
				if ((uint)index >= (uint)m_Cache.m_Length)
					FailOutOfRangeError(index);
#endif

                if (index < m_Cache.m_CachedBeginIndex || index >= m_Cache.m_CachedEndIndex)
                    m_Cache.UpdateCache(index);

				UnsafeUtility.WriteArrayElementWithStride (m_Cache.m_CachedPtr, index, m_Cache.m_CachedStride, value);
			}
		}

		void FailOutOfRangeError(int index)
		{
			//@TODO: Make error message utility and share with NativeArray...
#if ENABLE_NATIVE_ARRAY_CHECKS
			if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
				throw new IndexOutOfRangeException(string.Format("Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\nReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.", index, m_MinIndex, m_MaxIndex));
#endif

			throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
		}

		public int Length { get { return m_Length; } }
	}
}