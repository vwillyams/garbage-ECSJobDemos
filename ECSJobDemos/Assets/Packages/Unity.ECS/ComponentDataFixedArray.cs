using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace UnityEngine.ECS
{

	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	public unsafe struct ComponentDataFixedArray<T> where T : struct
	{
        ComponentDataArrayCache m_Cache;
        int                     m_CachedFixedArrayLength;
        int                     m_Length;


        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        int                      	m_MinIndex;
		int                      	m_MaxIndex;
		AtomicSafetyHandle       	m_Safety;
        #endif

		#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal unsafe ComponentDataFixedArray(ComponentDataArrayCache cache, int length, AtomicSafetyHandle safety, bool isReadOnly)
		#else
        internal unsafe ComponentDataFixedArray(ComponentDataArrayCache cache, int length)
		#endif
		{
            m_Length = length;
            m_Cache = cache;
			m_CachedFixedArrayLength = -1;

			#if ENABLE_UNITY_COLLECTIONS_CHECKS
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
				AtomicSafetyHandle safety = m_Safety;
#endif

				if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
				{
					m_Cache.UpdateCache(index);
					m_CachedFixedArrayLength = m_Cache.CachedSizeOf / UnsafeUtility.SizeOf<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					if (m_Cache.CachedSizeOf % UnsafeUtility.SizeOf<T>() != 0)
					{
						throw new System.InvalidOperationException("Fixed array size must be multiple of sizeof"); 
					}
#endif
				}

                IntPtr ptr = m_Cache.CachedPtr + index * m_Cache.CachedSizeOf;
                var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, m_CachedFixedArrayLength, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, safety);
#endif
                return array;
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
