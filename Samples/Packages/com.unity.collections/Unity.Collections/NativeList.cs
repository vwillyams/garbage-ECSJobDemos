﻿using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
using System.Diagnostics;
#endif

namespace Unity.Collections
{
	[StructLayout (LayoutKind.Sequential)]
	[NativeContainer]
	[DebuggerDisplay("Length = {Length}")]
	[DebuggerTypeProxy(typeof(NativeListDebugView < >))]
	public struct NativeList<T>
        where T : struct
	{
	    internal NativeListImpl<T, DefaultMemoryManager, NativeBufferSentinel> m_Impl;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	    internal AtomicSafetyHandle m_Safety;
#endif

	    public unsafe NativeList(Allocator i_label) : this (1, i_label, 1) { }
	    public unsafe NativeList(int capacity, Allocator i_label) : this (capacity, i_label, 1) { }

	    unsafe NativeList(int capacity, Allocator i_label, int stackDepth)
	    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        var guardian = new NativeBufferSentinel(stackDepth);
	        m_Impl = new NativeListImpl<T, DefaultMemoryManager, NativeBufferSentinel>(capacity, i_label, guardian);
	        m_Safety = AtomicSafetyHandle.Create();
#else
	        m_Impl = new NativeListImpl<T, DefaultMemoryManager, NativeBufferGuardian>(capacity, i_label);
#endif
	    }

	    public T this [int index]
		{
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Impl[index];

            }
	        set
	        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
	            m_Impl[index] = value;

	        }
		}

	    public int Length
	    {
	        get
	        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
	            return m_Impl.Length;
	        }
	    }

	    public int Capacity
	    {
	        get
	        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
	            return m_Impl.Capacity;
	        }

	        set
	        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
	            m_Impl.Capacity = value;
	        }
	    }

		public void Add(T element)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		    AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
		    m_Impl.Add(element);
		}

        public void AddRange(NativeArray<T> elements)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif

            m_Impl.AddRange(elements);
        }

		public void RemoveAtSwapBack(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		    AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);

            if( index < 0 || index >= Length )
                throw new ArgumentOutOfRangeException(index.ToString());
#endif
			m_Impl.RemoveAtSwapBack(index);
		}

		public bool IsCreated => !m_Impl.IsNull;

	    public void Dispose()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		    AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
		    AtomicSafetyHandle.Release(m_Safety);
#endif
		    m_Impl.Dispose();
		}

		public void Clear()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

		    m_Impl.Clear();
		}

	    public static implicit operator NativeArray<T> (NativeList<T> nativeList)
	    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        AtomicSafetyHandle arraySafety = new AtomicSafetyHandle();
	        AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(nativeList.m_Safety);
	        arraySafety = nativeList.m_Safety;
	        AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

	        var array = nativeList.m_Impl.ToNativeArray();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
	        return array;
	    }

		public T[] ToArray()
		{
		    NativeArray<T> nativeArray = this;
		    return nativeArray.ToArray();
		}

		public void CopyFrom(T[] array)
		{
		    //@TODO: Thats not right... This doesn't perform a resize
		    Capacity = array.Length;
		    NativeArray<T> nativeArray = this;
		    nativeArray.CopyFrom(array);
		}

		public void ResizeUninitialized(int length)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			m_Impl.ResizeUninitialized(length);
		}
	}


    sealed class NativeListDebugView<T> where T : struct
    {
        NativeList<T> m_Array;

        public NativeListDebugView(NativeList<T> array)
        {
            m_Array = array;
        }

        public T[] Items => m_Array.ToArray();
    }
}
namespace Unity.Collections.LowLevel.Unsafe
{
    public static class NativeListUnsafeUtility
    {
        public static unsafe void* GetUnsafePtr<T>(this NativeList<T> nativeList) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(nativeList.m_Safety);
#endif
            var data = nativeList.m_Impl.GetListData();
            return data->list;
        }
    }
}
