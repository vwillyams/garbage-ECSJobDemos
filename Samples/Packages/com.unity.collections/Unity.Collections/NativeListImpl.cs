using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Diagnostics;

namespace Unity.Collections
{

    /// <summary>
    /// What is this : struct that contains the data for a native list, that gets allocated using native memory allocation.
    /// Motivation(s): Need a single container struct to hold a native lists collection data.
    /// </summary>
	unsafe struct NativeListData
	{
		public void*                            list;
		public int								length;
		public int								capacity;
	}

    /// <summary>
    /// What is this : internal implementation of a variable size list, using native memory (not GC'd).
    /// Motivation(s): just need a resizable list that does not trigger the GC, for performance reasons.
    /// </summary>
    [StructLayout (LayoutKind.Sequential)]
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    public unsafe struct NativeListImpl<T, TMemManager, TSentinel>
        where TSentinel : struct, INativeBufferSentinel
#else
	public unsafe struct NativeListImpl<T, TAllocator>
#endif
        where T : struct
        where TMemManager : struct, INativeBufferMemoryManager
	{
        public TMemManager m_MemoryAllocator;

	    [NativeDisableUnsafePtrRestriction]
	    NativeListData* m_Data;

	    internal NativeListData* GetListData()
	    {
	        return m_Data;
	    }

	    public void* RawBuffer => m_Data;

	    public TMemManager Allocator => m_MemoryAllocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
	    internal TSentinel sentinel;
#endif

	    public T this [int index]
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if ((uint)index >= (uint)m_Data->length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{m_Data->length}' Length.");
#endif

                return UnsafeUtility.ReadArrayElement<T>(m_Data->list, index);
			}

			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			    if ((uint)index >= (uint)m_Data->length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range in NativeList of '{m_Data->length}' Length.");
#endif

                UnsafeUtility.WriteArrayElement(m_Data->list, index, value);
			}
		}

		public int Length
		{
			get
			{
				return m_Data->length;
			}
		}

		public int Capacity
		{
			get
			{
			    if( m_Data == null )
			        throw new NullReferenceException();
				return m_Data->capacity;
			}

			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			    if (value < m_Data->length)
			        throw new ArgumentException("Capacity must be larger than the length of the NativeList.");
#endif

				if (m_Data->capacity == value)
					return;

				void* newData = UnsafeUtility.Malloc (value * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_MemoryAllocator.Label);
				UnsafeUtility.MemCpy (newData, m_Data->list, m_Data->length * UnsafeUtility.SizeOf<T>());
				UnsafeUtility.Free (m_Data->list, m_MemoryAllocator.Label);
			    m_Data->list = newData;
			    m_Data->capacity = value;
			}
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		public NativeListImpl(int capacity, Allocator allocatorLabel, TSentinel sentinel)
#else
		public NativeListImpl(int capacity, Allocator allocatorLabel)
#endif
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		    this.sentinel = sentinel;
		    m_Data = null;

		    if (!UnsafeUtility.IsBlittable<T>())
		    {
		        this.sentinel.Dispose();
		        throw new ArgumentException(string.Format("{0} used in NativeList<{0}> must be blittable", typeof(T)));
		    }
#endif
		    m_MemoryAllocator = default(TMemManager);
		    m_Data = (NativeListData*)m_MemoryAllocator.Init<NativeListData>( allocatorLabel );

			var elementSize = UnsafeUtility.SizeOf<T> ();

            //@TODO: Find out why this is needed?
            capacity = Math.Max(1, capacity);
		    m_Data->list = UnsafeUtility.Malloc (capacity * elementSize, UnsafeUtility.AlignOf<T>(), allocatorLabel);

		    m_Data->length = 0;
		    m_Data->capacity = capacity;
		}

		public void Add(T element)
		{
			if (m_Data->length >= m_Data->capacity)
				Capacity = m_Data->length + m_Data->capacity * 2;

		    this[m_Data->length++] = element;
		}

        //@TODO: Test for AddRange
        public void AddRange(NativeArray<T> elements)
        {
            if (m_Data->length + elements.Length > m_Data->capacity)
                Capacity = m_Data->length + elements.Length * 2;

            var sizeOf = UnsafeUtility.SizeOf<T> ();
            UnsafeUtility.MemCpy((byte*)m_Data->list + m_Data->length * sizeOf, elements.GetUnsafePtr(), sizeOf * elements.Length);

            m_Data->length += elements.Length;
        }

		public void RemoveAtSwapBack(int index)
		{
		    var newLength = m_Data->length - 1;
			this[index] = this[newLength];
		    m_Data->length = newLength;
		}

		public bool IsNull => m_Data == null;

	    public void Dispose()
		{
		    if (m_Data != null)
		    {
		        sentinel.Dispose();

		        UnsafeUtility.Free (m_Data->list, m_MemoryAllocator.Label);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		        m_Data->list = (void*)0xDEADF00D;
#endif
		        m_MemoryAllocator.Dispose( m_Data );
                m_Data = null;
		    }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            else
		        throw new Exception("NativeList has yet to be allocated or has been dealocated!");
#endif
		}

		public void Clear()
		{
		    ResizeUninitialized (0);
		}

        /// <summary>
        /// Does NOT allocate memory, but shares it.
        /// </summary>
		public NativeArray<T> ToNativeArray()
		{
		    return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T> (m_Data->list, m_Data->length, Collections.Allocator.Invalid);
		}

		public void ResizeUninitialized(int length)
		{
		    Capacity = math.max(length, Capacity);
			m_Data->length = length;
		}

	    public NativeListImpl<T, TMemManager, TSentinel> Clone()
	    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        var clone = new NativeListImpl<T, TMemManager, TSentinel>( Capacity, m_MemoryAllocator.Label, sentinel);
#else
	        var clone = new NativeListImpl<T, TAllocator>(Capacity, m_NativeBuffer.m_AllocatorLabel);
#endif
            UnsafeUtility.MemCpy(clone.m_Data->list, m_Data->list, m_Data->length * UnsafeUtility.SizeOf<T>());
	        clone.m_Data->length = m_Data->length;

	        return clone;
	    }

	    public NativeArray<T> CopyToNativeArray(Allocator label)
	    {
	        var buffer = UnsafeUtility.Malloc( UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), label);
	        UnsafeUtility.MemCpy( buffer, m_Data->list, Length * UnsafeUtility.SizeOf<T>());
	        var copy = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T> (buffer, Length, label);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	        NativeArrayUnsafeUtility.SetAtomicSafetyHandle( ref copy, AtomicSafetyHandle.Create());
#endif
            return copy;
	    }
	}

}

