using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

//@TODO: ZERO TEST COVERAGE!!!

namespace ECS
{
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	public struct ComponentDataArray<T> where T : struct, IComponentData
	{
		unsafe int*                 m_Indices;
		IntPtr                   	m_Data;
		int                      	m_Length;

		#if ENABLE_NATIVE_ARRAY_CHECKS
		int                      	m_MinIndex;
		int                      	m_MaxIndex;
		AtomicSafetyHandle       	m_Safety;
		//@TODO: need safety for both data and indices... This is not safe...
		#endif

		public unsafe ComponentDataArray(NativeFreeList<T> data, NativeArray<int> indices)
		{
			m_Indices = (int*)indices.UnsafeReadOnlyPtr;
			m_Length = indices.Length;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			m_MinIndex = 0;
			m_MaxIndex = m_Length - 1;
			data.GetUnsafeBufferPointerWithoutChecksInternal(out m_Safety, out m_Data);
			#else
			m_Data = data.UnsafePtr;
			#endif
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
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);
				#endif

				int remapped = m_Indices[index];
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

				int remapped = m_Indices[index];
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
}