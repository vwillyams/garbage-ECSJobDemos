using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
using System.Diagnostics;
#endif

namespace Unity.Collections
{
	struct NativeListData
	{
		public System.IntPtr					list;
		public int								length;
		public int								capacity;
		
		public unsafe static void DeallocateList(IntPtr buffer, Allocator allocation)
		{
			NativeListData* data = (NativeListData*)buffer;
			UnsafeUtility.Free (data->list, allocation);
			data->list = IntPtr.Zero;
			UnsafeUtility.Free (buffer, allocation);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	[NativeContainer]
	public struct NativeList<T> where T : struct
	{
		internal System.IntPtr 			m_Buffer;
		Allocator 						m_AllocatorLabel;
		#if ENABLE_UNITY_COLLECTIONS_CHECKS
		internal AtomicSafetyHandle 	m_Safety;
		DisposeSentinel					m_DisposeSentinel;
		#endif

		unsafe public T this [int index]
		{
			get
			{
                NativeListData* data = (NativeListData*)m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                if ((uint)index >= (uint)data->length)
                    throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeList of '{1}' Length.", index, data->length));
#endif

                return UnsafeUtility.ReadArrayElement<T>(data->list, index);
			}

			set
			{
                NativeListData* data = (NativeListData*)m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                if ((uint)index >= (uint)data->length)
                    throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeList of '{1}' Length.", index, data->length));
#endif

                UnsafeUtility.WriteArrayElement<T>(data->list, index, value);
			}
		}

		unsafe public int Length
		{
			get
			{
				#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				return data->length;
			}
		}
			
		unsafe public int Capacity
		{
			get
			{
				#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				return data->capacity;
			}

			set
			{
				#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				if (data->capacity == value)
					return;
			
				IntPtr newData = UnsafeUtility.Malloc (value * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_AllocatorLabel);
				UnsafeUtility.MemCpy (newData, data->list, data->length * UnsafeUtility.SizeOf<T>());
				UnsafeUtility.Free (data->list, m_AllocatorLabel);
				data->list = newData;
				data->capacity = value;
			}
		}

		unsafe public NativeList(Allocator i_label) : this (1, i_label, 1) { }
		unsafe public NativeList(int capacity, Allocator i_label) : this (capacity, i_label, 1) { }

		unsafe private NativeList(int capacity, Allocator i_label, int stackDepth)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!UnsafeUtility.IsBlittable<T>())
                throw new ArgumentException(string.Format("{0} used in NativeList<{0}> must be blittable", typeof(T)));
#endif

            m_Buffer = UnsafeUtility.Malloc (sizeof(NativeListData), UnsafeUtility.AlignOf<NativeListData>(), i_label);
			NativeListData* data = (NativeListData*)m_Buffer;

			int elementSize = UnsafeUtility.SizeOf<T> ();

            //@TODO: Find out why this is needed?
            capacity = Math.Max(1, capacity);
			data->list = UnsafeUtility.Malloc (capacity * elementSize, UnsafeUtility.AlignOf<T>(), i_label);

			data->length = 0;
			data->capacity = capacity;

			m_AllocatorLabel = i_label;

#if ENABLE_UNITY_COLLECTIONS_CHECKS

            DisposeSentinel.Create(m_Buffer, i_label, out m_Safety, out m_DisposeSentinel, stackDepth, NativeListData.DeallocateList);
#endif
		}

		unsafe public void Add(T element)
		{			
			NativeListData* data = (NativeListData*)m_Buffer;
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
			#endif

			if (data->length >= data->capacity)
				Capacity = data->length + data->capacity * 2;

			int length = data->length;
			data->length = length + 1;
			this[length] = element;
		}

        //@TODO: Test for AddRange
        unsafe public void AddRange(NativeArray<T> elements)
        {   
            NativeListData* data = (NativeListData*)m_Buffer;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            #endif

            if (data->length + elements.Length > data->capacity)
                Capacity = data->length + elements.Length * 2;

            int sizeOf = UnsafeUtility.SizeOf<T> ();
            UnsafeUtility.MemCpy (data->list + data->length * sizeOf, elements.GetUnsafePtr(), sizeOf * elements.Length);

            data->length += elements.Length;
        }

		unsafe public void RemoveAtSwapBack(int index)
		{			
			NativeListData* data = (NativeListData*)m_Buffer;
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
			#endif

			int newLength = Length - 1;
			this[index] = this[newLength];
			data->length = newLength;
		}

		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		unsafe public void Dispose()
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS            
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
			#endif

			NativeListData.DeallocateList(m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}

		public void Clear()
		{
			ResizeUninitialized (0);
		}

		unsafe public static implicit operator NativeArray<T> (NativeList<T> nativeList)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle arraySafety = new AtomicSafetyHandle();
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(nativeList.m_Safety);
			arraySafety = nativeList.m_Safety;
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

			NativeListData* data = (NativeListData*)nativeList.m_Buffer;
			var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T> (data->list, data->length, Allocator.Invalid);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif

            return array;
		}

		unsafe public T[] ToArray()
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

		public unsafe void ResizeUninitialized(int length)
		{
			#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow (m_Safety);
			#endif

			Capacity = math.max(length, Capacity);
			NativeListData* data = (NativeListData*)m_Buffer;
			data->length = length;
		}
	}
}
namespace Unity.Collections.LowLevel.Unsafe
{
	static class NativeListUnsafeUtility
	{
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static unsafe IntPtr GetUnsafePtr<T>(this NativeList<T> nativeList) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(nativeList.m_Safety);
#endif
			NativeListData* data = (NativeListData*)nativeList.m_Buffer;
			return data->list;
        }
	}
}
