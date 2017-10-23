using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

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
        internal unsafe ComponentDataArray(ComponentDataArrayCache cache, int length, AtomicSafetyHandle safety, bool isReadOnly)
#else
        internal unsafe ComponentDataArray(ComponentDataArrayCache cache, int length)
#endif
        {
            m_Cache = cache;

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
#if ENABLE_NATIVE_ARRAY_CHECKS
            if (dst.Length == 0)
                return;

            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            //@TODO: Logic is weird. think about switching MaxIndex to end index???
            if (startIndex < m_MinIndex || startIndex + dst.Length > m_MaxIndex + 1)
                // @TODO: message is not accurate
                FailOutOfRangeError(startIndex + (dst.Length - 1));
#endif

            int elementSize = UnsafeUtility.SizeOf<T>();
            int copiedCount = 0;
            while (copiedCount < dst.Length)
            {
                int index = copiedCount + startIndex;
                m_Cache.UpdateCache(index);

                int copyCount = Math.Min(m_Cache.m_CachedEndIndex - index, dst.Length - copiedCount);

                if (m_Cache.m_CachedStride == elementSize && dst.Stride == elementSize)
                {
                    IntPtr srcPtr = m_Cache.m_CachedPtr + (index * elementSize);
                    IntPtr dstPtr = dst.UnsafePtr + (copiedCount * elementSize);
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, elementSize * copyCount);
                }
                else
                {
                    for (int i = 0; i != copyCount; i++)
                        dst[i + copiedCount] = UnsafeUtility.ReadArrayElementWithStride<T>(m_Cache.m_CachedPtr, i + index, m_Cache.m_CachedStride);
                }

                copiedCount += copyCount;
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
#endif

                if (index < m_Cache.m_CachedBeginIndex || index >= m_Cache.m_CachedEndIndex)
                    m_Cache.UpdateCache(index);

				UnsafeUtility.WriteArrayElementWithStride (m_Cache.m_CachedPtr, index, m_Cache.m_CachedStride, value);
			}
		}

#if ENABLE_NATIVE_ARRAY_CHECKS
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