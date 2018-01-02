using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

unsafe public struct BlobAllocator : IDisposable
{
	byte* 			    m_RootPtr;
	byte* 			    m_Ptr;

	long 			    m_Size;
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
	AtomicSafetyHandle  m_Safety;
	DisposeSentinel     m_DisposeSentinel;
	#endif

	//@TODO: make it so size tracking is automatic like BatchAllocator.h
	//@TODO: handle alignment correclty in the allocator
	public BlobAllocator (int sizeHint)
	{
	    //@TODO: Use virtual alloc to make it unnecessary to know the size ahead of time...
	    int size = 1024 * 1024 * 64;
		m_RootPtr = m_Ptr = (byte*)UnsafeUtility.Malloc (1024 * 1024 * 64, 16, Allocator.Persistent);
		m_Size = size;
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
		#endif
	}

    public void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
        UnsafeUtility.Free(m_RootPtr, Allocator.Persistent);
#endif
    }

    public unsafe void* ConstructRoot<T> () where T : struct
	{
	    byte* returnPtr = m_Ptr;
		m_Ptr += UnsafeUtility.SizeOf<T> ();
		return returnPtr;
	}

	unsafe int Allocate (long size, void* ptrAddr)
	{
		long offset = (byte*)ptrAddr - m_RootPtr;
		if (m_Ptr - m_RootPtr > m_Size)
			throw new System.ArgumentException("BlobAllocator.preallocated size not large enough");

		if (offset < 0 || offset + size > m_Size)
			throw new System.ArgumentException("Ptr must be part of root compound");

		byte* returnPtr = m_Ptr;
		m_Ptr += size;

	    long relativeOffset = returnPtr - (byte*)ptrAddr;
	    if (relativeOffset > int.MaxValue || relativeOffset < int.MinValue)
	        throw new System.ArgumentException("BlobPtr uses 32 bit offsets, and this offset exceeds it.");

		return (int)relativeOffset;
	}

	public void Allocate<T> (int length, ref BlobArray<T> ptr) where T : struct
	{
		ptr.m_OffsetPtr = Allocate(UnsafeUtility.SizeOf<T>() * length, UnsafeUtility.AddressOf(ref ptr));
		ptr.m_Length = length;
	}

	public void Allocate<T> (ref BlobPtr<T> ptr) where T : struct
	{
		ptr.m_OffsetPtr = Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AddressOf(ref ptr));
	}

    public BlobAssetReference<T> CreateBlobAssetReference<T>(Allocator allocator) where T : struct
    {
        Assert.AreEqual(16, sizeof(BlobAssetHeader));

        long dataSize = (m_Ptr - m_RootPtr);
        byte* buffer = (byte*)UnsafeUtility.Malloc(sizeof(BlobAssetHeader) + dataSize, 16, allocator);
        UnsafeUtility.MemCpy(buffer + sizeof(BlobAssetHeader), m_RootPtr, dataSize);

        BlobAssetHeader* header = (BlobAssetHeader*)buffer;
        *header = new BlobAssetHeader();
        header->Refcount = 1;
        header->Allocator = allocator;

        BlobAssetReference<T> assetReference;
        assetReference.m_Ptr = buffer + sizeof(BlobAssetHeader);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        assetReference.m_Safety = AtomicSafetyHandle.Create();
        AtomicSafetyHandle.SetAllowSecondaryVersionWriting(assetReference.m_Safety , false);
        AtomicSafetyHandle.UseSecondaryVersion(ref assetReference.m_Safety );
#endif

        return assetReference;
    }
}

struct BlobAssetHeader
{
    uint             _padding0;
    uint             _padding1;

    public int       Refcount;
    public Allocator Allocator;
}

[NativeContainer]
unsafe public struct BlobAssetReference<T> where T : struct
{
    [NativeDisableUnsafePtrRestriction]
    internal byte*				        m_Ptr;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal AtomicSafetyHandle 	m_Safety;
#endif

    public void* GetUnsafeReadOnlyPtr()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        return m_Ptr;
    }

    public void Retain()
    {
        var header = (BlobAssetHeader*)m_Ptr;
        header -= 1;
        Interlocked.Increment(ref header->Refcount);
    }

    public void SafeRelease()
    {
        if (m_Ptr != null)
            Release();
    }

    public void Release()
    {
        var header = (BlobAssetHeader*)m_Ptr;
        header -= 1;

        if (Interlocked.Decrement(ref header->Refcount) == 1)
        {
            var res = AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_Safety);
            UnsafeUtility.Free(header, header->Allocator);

            if (res != EnforceJobResult.AllJobsAlreadySynced)
            {
                Debug.LogError("Resource was already destroyed");
            }
            m_Ptr = null;
        }
    }
}

unsafe public struct BlobPtr<T> where T : struct
{
	internal int	m_OffsetPtr;

	public unsafe T Value
	{
		get
		{
		    fixed (int* thisPtr = &m_OffsetPtr)
		    {
		        T val;
		        UnsafeUtility.CopyPtrToStructure((byte*)thisPtr + m_OffsetPtr, out val);
		        return val;
		    }
		}
		set
		{
		    fixed (int* thisPtr = &m_OffsetPtr)
		    {
		        UnsafeUtility.CopyStructureToPtr(ref value, (byte*)thisPtr + m_OffsetPtr);
		    }
		}
	}

	public unsafe void* UnsafePtr
	{
		get
		{
		    fixed (int* thisPtr = &m_OffsetPtr)
		    {
		        return (byte*)thisPtr + m_OffsetPtr;
		    }
		}
	}
}

unsafe public struct BlobArray<T> where T : struct
{
    internal int					m_OffsetPtr;
    internal int 				    m_Length;

	public int Length { get { return m_Length; } }

	public unsafe void* UnsafePtr
	{
		get
		{
		    fixed (int* thisPtr = &m_OffsetPtr)
		    {
		        return (byte*)thisPtr + m_OffsetPtr;
		    }
		}
	}

	public unsafe T this [int index]
	{
		get
		{
			if ((uint)index >= (uint)m_Length)
				throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range Length {1}", index, m_Length));

		    fixed (int* thisPtr = &m_OffsetPtr)
		    {
		        return UnsafeUtility.ReadArrayElement<T>((byte*)thisPtr + m_OffsetPtr, index);
		    }
		}
		set
		{
			if ((uint)index >= (uint)m_Length)
				throw new System.IndexOutOfRangeException ();

		    fixed (int* thisPtr = &m_OffsetPtr)
		    {
		        UnsafeUtility.WriteArrayElement<T>((byte*)thisPtr + m_OffsetPtr, index, value);
		    }
		}
	}
}
