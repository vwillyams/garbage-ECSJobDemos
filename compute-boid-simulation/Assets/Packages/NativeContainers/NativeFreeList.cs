using System;
using System.Runtime.InteropServices;
using UnityEngine;

#if ENABLE_NATIVE_ARRAY_CHECKS
using System.Diagnostics;
#endif
namespace UnityEngine.Collections
{
	[StructLayout (LayoutKind.Sequential)]
	[NativeContainer]
	public struct NativeFreeList<T> where T : struct
	{
		System.IntPtr 					m_Buffer;
		int								m_SizeOf;
		int 							m_Length;
		int 							m_Capacity;
		int								m_NextFree;
		Allocator 						m_AllocatorLabel;
		#if ENABLE_NATIVE_ARRAY_CHECKS
		AtomicSafetyHandle 				m_Safety;
		DisposeSentinel					m_DisposeSentinel;
		#endif

		const int kNullLinkId = -1;

		//@TODO: Debugging system to track which indices are active or not...
		unsafe public T this [int index]
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				if ((uint)index >= (uint)m_Capacity)
					throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeFreeList of '{1}' Capacity.", index, m_Capacity));

				return UnsafeUtility.ReadArrayElementWithStride<T>(m_Buffer, index, m_SizeOf);
			}

			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif

				if ((uint)index >= (uint)m_Capacity)
					throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeFreeList of '{1}' Capacity.", index, m_Capacity));

				UnsafeUtility.WriteArrayElementWithStride<T>(m_Buffer, index, m_SizeOf, value);
			}
		}

		unsafe public int Length
		{
			get
			{
				return m_Length;
			}
		}
			
		unsafe public int Capacity
		{
			get
			{
				return m_Capacity;
			}

			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif

				if (value <= m_Capacity)
					return;

				int alignOf = math.max (UnsafeUtility.AlignOf<T> (), sizeof(int));

				IntPtr newData = UnsafeUtility.Malloc (value * m_SizeOf, alignOf, m_AllocatorLabel);
				UnsafeUtility.MemCpy (newData, m_Buffer, m_Capacity * m_SizeOf);
				UnsafeUtility.Free (m_Buffer, m_AllocatorLabel);
				m_Buffer = newData;

				#if ENABLE_NATIVE_ARRAY_CHECKS
				DisposeSentinel.UpdateBufferPtr(m_DisposeSentinel, m_Buffer);
				#endif
				for (int i = m_Capacity; i < value - 1; ++i)
					UnsafeUtility.WriteArrayElementWithStride<int> (m_Buffer, i, m_SizeOf, i + 1);

				UnsafeUtility.WriteArrayElementWithStride<int> (m_Buffer, value-1, m_SizeOf, kNullLinkId);

				m_NextFree = m_Capacity;
				m_Capacity = value;
			}
		}

		unsafe public NativeFreeList(Allocator i_label)
		{
			m_SizeOf = math.max (UnsafeUtility.SizeOf<T> (), sizeof(int));
			m_Buffer = IntPtr.Zero;

			m_Capacity = 0;

			m_NextFree = kNullLinkId;
			m_Length = 0;
			m_AllocatorLabel = i_label;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			DisposeSentinel.Create(m_Buffer, i_label, out m_Safety, out m_DisposeSentinel, 0);
			AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_Safety, false);
			#endif
		}

		unsafe public int Add(T value)
		{			
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif

			if (m_NextFree == kNullLinkId)
				Capacity = m_Capacity != 0 ? 2 * m_Capacity : 4;

			int id = m_NextFree;
			m_NextFree = UnsafeUtility.ReadArrayElementWithStride<int> (m_Buffer, id, m_SizeOf);
			m_Length++;

			UnsafeUtility.WriteArrayElementWithStride (m_Buffer, id, m_SizeOf, value);

			return id;
		}

		unsafe public void Add(T value, NativeSlice<int> outputIndices)
		{			
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif

			int* outIndicesPtr = (int*)outputIndices.UnsafePtr;
			int count = outputIndices.Length;

			if (m_Length + count > m_Capacity)
				Capacity = (count + m_Length) * 2;

			m_Length += count;

			for (int i = 0; i != count; i++)
			{
				int id = m_NextFree;
				m_NextFree = UnsafeUtility.ReadArrayElementWithStride<int> (m_Buffer, id, m_SizeOf);

				UnsafeUtility.WriteArrayElementWithStride (m_Buffer, id, m_SizeOf, value);

				outIndicesPtr[i] = id;
			}
		}


		unsafe public void Remove(int index)
		{			
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif

			//@TODO: Debugging system to track which indices are active or not...
			if ((uint)index >= (uint)m_Capacity)
				throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeFreeList of '{1}' Capacity.", index, m_Capacity));

			UnsafeUtility.WriteArrayElementWithStride<int> (m_Buffer, index, m_SizeOf, m_NextFree);
			m_NextFree = index;
			m_Length--;
		}

		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		unsafe public void Dispose()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS            
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
			#endif

			UnsafeUtility.Free (m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}

		public void GetUnsafeBufferPointerWithoutChecksInternal(out AtomicSafetyHandle handle, out IntPtr ptr)
		{
			ptr = m_Buffer;
			#if ENABLE_NATIVE_ARRAY_CHECKS
			handle = m_Safety;
			#else
			handle = new AtomicSafetyHandle();
			#endif
		}
	}
}
