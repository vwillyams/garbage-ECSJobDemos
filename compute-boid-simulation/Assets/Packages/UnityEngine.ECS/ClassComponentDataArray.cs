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
	#if ECS_ENTITY_CLASS
	public struct ComponentDataIndexSegment
	{
		public unsafe int* indices;
		public int beginIndex;
		public int endIndex;
		public int offset;
		public int stride;
	}
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	public struct ComponentDataArray<T> where T : struct, IComponentData
	{
		unsafe ComponentDataIndexSegment* m_IndexSegments;
		int m_NumIndexSegments;
		int m_PreviousIndexSegment;
		IntPtr                   	m_Data;
		int                      	m_Length;

		#if ENABLE_NATIVE_ARRAY_CHECKS
		int                      	m_MinIndex;
		int                      	m_MaxIndex;
		AtomicSafetyHandle       	m_Safety;
		//@TODO: need safety for both data and indices... This is not safe...
		#endif

		public unsafe ComponentDataArray(NativeFreeList<T> data, NativeArray<ComponentDataIndexSegment> indices, bool isReadOnly)
		{
			m_IndexSegments = (ComponentDataIndexSegment*)indices.UnsafeReadOnlyPtr;
			m_NumIndexSegments = indices.Length;
			m_Length = m_IndexSegments[m_NumIndexSegments-1].endIndex;
			m_PreviousIndexSegment = 0;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			m_MinIndex = 0;
			m_MaxIndex = m_Length - 1;
			data.GetUnsafeBufferPointerWithoutChecksInternal(out m_Safety, out m_Data);
			if (isReadOnly)
				AtomicSafetyHandle.UseSecondaryVersion(ref m_Safety);
			#else
			m_Data = data.UnsafePtr;
			#endif
		}

		public unsafe T this[int index]
		{
			get
			{

				#if ENABLE_NATIVE_ARRAY_CHECKS_FOO
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
				#else
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);
				#endif

				if (index >= m_IndexSegments[m_PreviousIndexSegment].endIndex || index < m_IndexSegments[m_PreviousIndexSegment].beginIndex)
				{
					for (int i = 0; i < m_NumIndexSegments; ++i)
					{
						if (index >= m_IndexSegments[i].beginIndex && index < m_IndexSegments[i].endIndex)
						{
							m_PreviousIndexSegment = i;
							break;
						}
					}
				}
				int remapped = m_IndexSegments[m_PreviousIndexSegment].indices[m_IndexSegments[m_PreviousIndexSegment].offset + (index - m_IndexSegments[m_PreviousIndexSegment].beginIndex)*m_IndexSegments[m_PreviousIndexSegment].stride];
				return UnsafeUtility.ReadArrayElement<T> (m_Data, remapped);
			}
			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				if (index < m_MinIndex || index > m_MaxIndex)
					FailOutOfRangeError(index);
				#else
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);
				#endif

				if (index >= m_IndexSegments[m_PreviousIndexSegment].endIndex || index < m_IndexSegments[m_PreviousIndexSegment].beginIndex)
				{
					for (int i = 0; i < m_NumIndexSegments; ++i)
					{
						if (index >= m_IndexSegments[i].beginIndex && index < m_IndexSegments[i].endIndex)
						{
							m_PreviousIndexSegment = i;
							break;
						}
					}
				}
				int remapped = m_IndexSegments[m_PreviousIndexSegment].indices[m_IndexSegments[m_PreviousIndexSegment].offset + (index - m_IndexSegments[m_PreviousIndexSegment].beginIndex)*m_IndexSegments[m_PreviousIndexSegment].stride];
				UnsafeUtility.WriteArrayElement (m_Data, remapped, value);
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
	#endif // ECS_ENTITY_CLASS
}