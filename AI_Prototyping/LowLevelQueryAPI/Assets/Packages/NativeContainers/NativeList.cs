using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if ENABLE_NATIVE_ARRAY_CHECKS
using System.Diagnostics;
#endif
namespace UnityEngine.Collections
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
		System.IntPtr 					m_Buffer;
		Allocator 						m_AllocatorLabel;
		#if ENABLE_NATIVE_ARRAY_CHECKS
		AtomicSafetyHandle 				m_Safety;
		DisposeSentinel					m_DisposeSentinel;
		#endif

		unsafe public T this [int index]
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				if ((uint)index >= (uint)data->length)
					throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeList of '{1}' Length.", index, data->length));

				return UnsafeUtility.ReadArrayElement<T>(data->list, index);
			}

			set
			{

				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				if ((uint)index >= (uint)data->length)
					throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range in NativeList of '{1}' Length.", index, data->length));

				UnsafeUtility.WriteArrayElement<T>(data->list, index, value);
			}
		}

		unsafe public int Length
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
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
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				return data->capacity;
			}

			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
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
			m_Buffer = UnsafeUtility.Malloc (sizeof(NativeListData), UnsafeUtility.AlignOf<NativeListData>(), i_label);
			NativeListData* data = (NativeListData*)m_Buffer;

			int elementSize = UnsafeUtility.SizeOf<T> ();

			//@TODO: Find out why this is needed?
			capacity = Mathf.Max(capacity, 1);
			data->list = UnsafeUtility.Malloc (capacity * elementSize, UnsafeUtility.AlignOf<T>(), i_label);

			data->length = 0;
			data->capacity = capacity;

			m_AllocatorLabel = i_label;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			DisposeSentinel.Create(m_Buffer, i_label, out m_Safety, out m_DisposeSentinel, stackDepth, NativeListData.DeallocateList);
			#endif
		}

		unsafe public void Add(T element)
		{			
			NativeListData* data = (NativeListData*)m_Buffer;
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
			#endif

			if (data->length >= data->capacity)
				Capacity = data->length + data->capacity * 2;

			int length = data->length;
			data->length = length + 1;
			this[length] = element;
		}

		unsafe public void RemoveAtSwapBack(int index)
		{			
			NativeListData* data = (NativeListData*)m_Buffer;
			#if ENABLE_NATIVE_ARRAY_CHECKS
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
			#if ENABLE_NATIVE_ARRAY_CHECKS            
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
			AtomicSafetyHandle arraySafety = new AtomicSafetyHandle();
#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(nativeList.m_Safety);
			arraySafety = nativeList.m_Safety;
			AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

			NativeListData* data = (NativeListData*)nativeList.m_Buffer;
			return NativeArray<T>.ConvertExistingDataToNativeArrayInternal (data->list, data->length, arraySafety, Allocator.Invalid);
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

		public unsafe System.IntPtr UnsafePtr
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow (m_Safety);
				#endif

				NativeListData* data = (NativeListData*)m_Buffer;
				return data->list;
			}
		}

		public unsafe void ResizeUninitialized(int length)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow (m_Safety);
			#endif

			Capacity = math.max(length, Capacity);
			NativeListData* data = (NativeListData*)m_Buffer;
			data->length = length;
		}
	}
}
