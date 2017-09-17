using System;
using System.Runtime.InteropServices;
using UnityEngine;

#if ENABLE_NATIVE_ARRAY_CHECKS
using System.Diagnostics;
#endif
namespace UnityEngine.Collections
{
	public struct NativePoolAllocator
	{
        IntPtr 					        m_Buffer;
		int								m_SizeOf;
		int 							m_Capacity;
		int								m_NextFree;
		Allocator 						m_AllocatorLabel;

		const int kNullLinkId = -1;

		unsafe public NativePoolAllocator(int capacity, int sizeOf, int alignOf, Allocator i_label)
		{
			m_SizeOf = math.max (sizeOf, sizeof(int));
			m_Buffer = IntPtr.Zero;

			m_AllocatorLabel = i_label;

			m_Buffer = UnsafeUtility.Malloc (capacity * m_SizeOf, alignOf, m_AllocatorLabel);

			for (int i = 0; i < capacity - 1; ++i)
				UnsafeUtility.WriteArrayElementWithStride<int> (m_Buffer, i, m_SizeOf, i + 1);

			UnsafeUtility.WriteArrayElementWithStride<int> (m_Buffer, capacity-1, m_SizeOf, kNullLinkId);

			m_Capacity = capacity;
			m_NextFree = 0;
		}

		unsafe public IntPtr Allocate()
		{			
			if (m_NextFree == kNullLinkId)
				return IntPtr.Zero;

			int id = m_NextFree;
			m_NextFree = UnsafeUtility.ReadArrayElementWithStride<int> (m_Buffer, id, m_SizeOf);

			return m_Buffer + id * m_SizeOf;
		}

		unsafe public void Deallocate(IntPtr ptr)
		{		
            int byteDistance = (int)((byte*)ptr - (byte*)m_Buffer);
            int index = byteDistance / m_SizeOf;

			UnsafeUtility.WriteArrayElementWithStride<int> (m_Buffer, index, m_SizeOf, m_NextFree);
			m_NextFree = index;
		}

		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		unsafe public void Dispose()
		{
			UnsafeUtility.Free (m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}
	}
}
