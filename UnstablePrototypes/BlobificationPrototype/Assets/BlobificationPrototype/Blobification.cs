using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

unsafe public struct BlobAllocator
{
	byte* 			    m_RootPtr;
	byte* 			    m_Ptr;
	long 			    m_Size;
	Allocator 		    m_Label;
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
	AtomicSafetyHandle  m_Safety;
	DisposeSentinel     m_DisposeSentinel;
	#endif

	//@TODO: make it so size tracking is automatic like BatchAllocator.h
	//@TODO: handle alignment correclty in the allocator
	public BlobAllocator (Allocator label, int size)
	{
		m_RootPtr = m_Ptr = (byte*)UnsafeUtility.Malloc (size, 16, label);
		m_Size = size;
		m_Label = label;
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
		#endif
	}

	public unsafe void* ConstructRoot<T> () where T : struct
	{
	    byte* returnPtr = m_Ptr;
		m_Ptr += UnsafeUtility.SizeOf<T> ();
		return returnPtr;
	}

	unsafe byte* Allocate (long size, void* ptrAddr)
	{
		long offset = (byte*)ptrAddr - m_RootPtr;
		if (m_Ptr - m_RootPtr > m_Size)
			throw new System.ArgumentException("BlobAllocator.preallocated size not large enough");

		if (offset < 0 || offset + size > m_Size)
			throw new System.ArgumentException("Ptr must be part of root compound");

		byte* returnPtr = m_Ptr ;
		m_Ptr += size;

		return returnPtr;
	}

	public unsafe void Allocate<T> (int length, ref BlobArray<T> ptr) where T : struct
	{
		ptr.m_Ptr = Allocate(UnsafeUtility.SizeOf<T>() * length, UnsafeUtility.AddressOf(ref ptr));
		ptr.m_Length = length;
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		ptr.m_Safety = m_Safety;
		#endif
	}

	public unsafe void Allocate<T> (ref BlobPtr<T> ptr) where T : struct
	{
		ptr.m_Ptr = Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AddressOf(ref ptr));
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		ptr.m_Safety = m_Safety;
		#endif
	}

    //@TODO: Rename Commit?
	public BlobRootPtr<T> Create<T>() where T : struct
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		var value = new BlobRootPtr<T>(m_RootPtr, m_Label, m_Safety, m_DisposeSentinel);
		#else
		var value = new BlobRootPtr<T>(m_RootPtr, m_Label);
		#endif
		return value;
	}
}

[NativeContainer]
unsafe public struct BlobRootPtr<T> where T : struct
{
    [NativeDisableUnsafePtrRestriction]
	public byte*				m_Ptr;
	public Allocator 			m_Label;

	#if ENABLE_UNITY_COLLECTIONS_CHECKS
	public AtomicSafetyHandle 	m_Safety;
    [NativeSetClassTypeToNullOnSchedule]
	public DisposeSentinel 		m_DisposeSentinel;
	#endif

	#if ENABLE_UNITY_COLLECTIONS_CHECKS
	internal BlobRootPtr(byte* memory, Allocator label, AtomicSafetyHandle handle, DisposeSentinel disposeSentinel)
	{
		m_Ptr = memory;
		m_Label = label;
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		m_DisposeSentinel = disposeSentinel;
		m_Safety = handle;
		#endif
	}
	#else
	internal BlobRootPtr(IntPtr memory, Allocator label)
	{
		m_Ptr = memory;
		m_Label = label;
	}
	#endif

	public unsafe IntPtr UnsafeReadOnlyPtr
	{
		get
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			#endif

			return (IntPtr)m_Ptr;
		}
	}
	public unsafe IntPtr UnsafePtr
	{
		get
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif

			return (IntPtr)m_Ptr;
		}
	}

	public void Dispose()
	{
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
		#endif

		UnsafeUtility.Free (m_Ptr, m_Label);
		m_Ptr = null;
	}
}

unsafe public struct BlobPtr<T> where T : struct
{
	public byte*					m_Ptr;
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
	public AtomicSafetyHandle 		m_Safety;
	#endif

	public unsafe T Value
	{
		get
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			T val;
			UnsafeUtility.CopyPtrToStructure(m_Ptr, out val);
			return val;
		}
		set
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			UnsafeUtility.CopyStructureToPtr(ref value, m_Ptr);
		}
	}

	public unsafe IntPtr UnsafePtr
	{
		get
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			return (IntPtr)m_Ptr;
		}
	}
}

unsafe public struct BlobArray<T> where T : struct
{
	public byte*					m_Ptr;
	public int 						m_Length;
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
	public AtomicSafetyHandle 		m_Safety;
	#endif

	public int Length { get { return m_Length; } }

	public unsafe IntPtr UnsafePtr
	{
		get
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			return (IntPtr)m_Ptr;
		}
	}

	public unsafe T this [int index]
	{
		get
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			if ((uint)index >= (uint)m_Length)
				throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range Length {1}", index, m_Length));

			return UnsafeUtility.ReadArrayElement<T>(m_Ptr, index);
		}
		set
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			if ((uint)index >= (uint)m_Length)
				throw new System.IndexOutOfRangeException ();

			UnsafeUtility.WriteArrayElement<T>(m_Ptr, index, value);
		}
	}
}
