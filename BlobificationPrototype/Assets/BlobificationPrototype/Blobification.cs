using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Jobs;
using UnityEngine.Collections;

public struct BlobAllocator
{
	long 			m_RootPtr;
	long 			m_Ptr;
	long 			m_Size;
	Allocator 		m_Label;
	#if ENABLE_NATIVE_ARRAY_CHECKS
	AtomicSafetyHandle m_Safety;
	DisposeSentinel m_DisposeSentinel;
	#endif

	//@TODO: make it so size tracking is automatic like BatchAllocator.h
	//@TODO: handle alignment correclty in the allocator
	public BlobAllocator (Allocator label, int size)
	{
		m_RootPtr = m_Ptr = (long)UnsafeUtility.Malloc (size, 16, label);
		m_Size = size;
		m_Label = label;
		#if ENABLE_NATIVE_ARRAY_CHECKS
		DisposeSentinel.Create((IntPtr)m_Ptr, m_Label, out m_Safety, out m_DisposeSentinel, 0);
		#endif
	}

	public unsafe IntPtr ConstructRoot<T> () where T : struct
	{
		IntPtr ptr = (IntPtr)m_Ptr;
		m_Ptr += UnsafeUtility.SizeOf<T> ();

		return ptr;
	}

	unsafe IntPtr Allocate (long size, IntPtr ptrAddr)
	{
		long offset = ptrAddr.ToInt64() - m_RootPtr;
		if (m_Ptr - m_RootPtr > m_Size)
			throw new System.ArgumentException("BlobAllocator.preallocated size not large enough");

		if (offset < 0 || offset + size > m_Size)
			throw new System.ArgumentException("Ptr must be part of root compound");

		IntPtr returnPtr = (IntPtr)m_Ptr;
		m_Ptr += size;

		return returnPtr;
	}

	public unsafe void Allocate<T> (int length, ref BlobArray<T> ptr) where T : struct
	{
		ptr.m_Ptr = Allocate(UnsafeUtility.SizeOf<T>() * length, UnsafeUtility.AddressOf(ref ptr));
		ptr.m_Length = length;
		#if ENABLE_NATIVE_ARRAY_CHECKS
		ptr.m_Safety = m_Safety;
		#endif
	}

	public unsafe void Allocate<T> (ref BlobPtr<T> ptr) where T : struct
	{
		ptr.m_Ptr = Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AddressOf(ref ptr));
		#if ENABLE_NATIVE_ARRAY_CHECKS
		ptr.m_Safety = m_Safety;
		#endif
	}

	public BlobRootPtr<T> Create<T>() where T : struct
	{
		#if ENABLE_NATIVE_ARRAY_CHECKS
		var value = new BlobRootPtr<T>((IntPtr)m_RootPtr, m_Label, m_Safety, m_DisposeSentinel);
		#else
		var value = new BlobRootPtr<T>((IntPtr)m_RootPtr, m_Label);
		#endif
		return value;
	}
}

[NativeContainer]
public struct BlobRootPtr<T> where T : struct
{
	public IntPtr				m_Ptr;
	public Allocator 			m_Label;

	#if ENABLE_NATIVE_ARRAY_CHECKS
	public AtomicSafetyHandle 	m_Safety;
	public DisposeSentinel 		m_DisposeSentinel;
	#endif

	#if ENABLE_NATIVE_ARRAY_CHECKS
	internal BlobRootPtr(IntPtr memory, Allocator label, AtomicSafetyHandle handle, DisposeSentinel disposeSentinel)
	{
		m_Ptr = memory;
		m_Label = label;
		#if ENABLE_NATIVE_ARRAY_CHECKS
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
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			#endif

			return m_Ptr;
		}
	}
	public unsafe IntPtr UnsafePtr
	{
		get
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif

			return m_Ptr;
		}
	}

	public void Dispose()
	{
		#if ENABLE_NATIVE_ARRAY_CHECKS
		DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
		#endif

		UnsafeUtility.Free (m_Ptr, m_Label);
		m_Ptr = IntPtr.Zero;
	}
}

public struct BlobPtr<T> where T : struct
{
	public IntPtr					m_Ptr;
	#if ENABLE_NATIVE_ARRAY_CHECKS
	public AtomicSafetyHandle 		m_Safety;
	#endif

	public unsafe T Value
	{
		get
		{ 
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			T val;
			UnsafeUtility.CopyPtrToStructure(m_Ptr, out val);
			return val;
		}
		set
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			UnsafeUtility.CopyStructureToPtr(ref value, m_Ptr);
		}
	}

	public unsafe IntPtr UnsafePtr
	{
		get
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			return m_Ptr;
		}
	}
}

public struct BlobArray<T> where T : struct
{
	public IntPtr					m_Ptr;
	public int 						m_Length;
	#if ENABLE_NATIVE_ARRAY_CHECKS
	public AtomicSafetyHandle 		m_Safety;
	#endif

	public int Length { get { return m_Length; } }

	public unsafe IntPtr UnsafePtr 
	{
		get
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			return m_Ptr;
		}
	}
		
	public unsafe T this [int index]
	{
		get
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			if ((uint)index >= (uint)m_Length)
				throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range Length {1}", index, m_Length));
			
			return UnsafeUtility.ReadArrayElement<T>(m_Ptr, index);
		}
		set
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			#endif
			if ((uint)index >= (uint)m_Length)
				throw new System.IndexOutOfRangeException ();

			UnsafeUtility.WriteArrayElement<T>(m_Ptr, index, value);
		}
	}
}