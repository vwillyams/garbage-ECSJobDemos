using UnityEngine;
using UnityEngine.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{

	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	public unsafe struct ComponentDataFixedArray<T> where T : struct
	{
        ComponentDataArrayCache m_Cache;
        int                     m_FixedArrayLength;
        int                     m_Length;


        #if ENABLE_NATIVE_ARRAY_CHECKS
        int                      	m_MinIndex;
		int                      	m_MaxIndex;
		AtomicSafetyHandle       	m_Safety;
        #endif

		#if ENABLE_NATIVE_ARRAY_CHECKS
        internal unsafe ComponentDataFixedArray(ComponentDataArrayCache cache, int length, int fixedArrayLength, AtomicSafetyHandle safety, bool isReadOnly)
		#else
        internal unsafe ComponentDataFixedArray(ComponentDataArrayCache cache, int length, int fixedArrayLength)
		#endif
		{
            m_Length = length;
            m_Cache = cache;
            m_FixedArrayLength = fixedArrayLength;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			m_MinIndex = 0;
			m_MaxIndex = length - 1;
			m_Safety = safety;
			if (isReadOnly)
				AtomicSafetyHandle.UseSecondaryVersion(ref m_Safety);
			#endif

		}

		public unsafe NativeArray<T> this[int index]
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

                IntPtr ptr = m_Cache.CachedPtr + (index * m_Cache.CachedStride);
                return NativeArray<T>.ConvertExistingDataToNativeArrayInternal(ptr, m_FixedArrayLength, m_Safety, Allocator.Invalid);
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