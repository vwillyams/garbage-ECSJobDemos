using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

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

                int copyCount = Math.Min(m_Cache.CachedEndIndex - index, dst.Length - copiedCount);

                if (m_Cache.CachedStride == elementSize && dst.Stride == elementSize)
                {
                    IntPtr srcPtr = m_Cache.CachedPtr + (index * elementSize);
                    IntPtr dstPtr = dst.GetUnsafePtr() + (copiedCount * elementSize);
                    UnsafeUtility.MemCpy(dstPtr, srcPtr, elementSize * copyCount);
                }
                else
                {
                    for (int i = 0; i != copyCount; i++)
                        dst[i + copiedCount] = UnsafeUtility.ReadArrayElementWithStride<T>(m_Cache.CachedPtr, i + index, m_Cache.CachedStride);
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

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
                    m_Cache.UpdateCache(index);

                return UnsafeUtility.ReadArrayElementWithStride<T>(m_Cache.CachedPtr, index, m_Cache.CachedStride);
            }

			set
			{
#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
#endif

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
                    m_Cache.UpdateCache(index);

				UnsafeUtility.WriteArrayElementWithStride (m_Cache.CachedPtr, index, m_Cache.CachedStride, value);
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