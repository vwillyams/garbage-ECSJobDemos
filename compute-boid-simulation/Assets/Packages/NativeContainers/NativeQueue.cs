using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if ENABLE_NATIVE_ARRAY_CHECKS
using System.Diagnostics;
#endif
using System.Threading;

namespace UnityEngine.Collections
{
	[StructLayout (LayoutKind.Sequential)]
	internal unsafe struct QueueData
	{
		public System.IntPtr m_Data;
		public int m_NextWritePos;
		public int m_NextReadPos;
		public int m_Capacity;

		public unsafe static void AllocateQueue<T>(int capacity, Allocator label, out IntPtr outBuf) where T : struct
		{
			outBuf = UnsafeUtility.Malloc(UnsafeUtility.SizeOf<QueueData>(), UnsafeUtility.AlignOf<QueueData>(), label);
			QueueData* data = (QueueData*)outBuf;
			data->m_NextWritePos = 0;
			data->m_NextReadPos = 0;
			data->m_Capacity = capacity;
			data->m_Data = UnsafeUtility.Malloc(capacity * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), label);
		}
		public unsafe static void ReallocateQueue<T>(QueueData* data, int newCapacity, Allocator label) where T : struct
		{
			if (data->m_Capacity == newCapacity)
				return;

			if (data->m_Capacity > newCapacity)
				throw new System.Exception("Shrinking a queue is not supported");

			IntPtr newData = UnsafeUtility.Malloc(newCapacity * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), label);

			if (data->m_NextWritePos < data->m_NextReadPos)
			{
				int dataLen = data->m_Capacity - data->m_NextReadPos;
				UnsafeUtility.MemCpy(newData, (IntPtr)((Byte*)data->m_Data + data->m_NextReadPos * UnsafeUtility.SizeOf<T>()), dataLen * UnsafeUtility.SizeOf<T>());				

				UnsafeUtility.MemCpy((IntPtr)((Byte*)newData + dataLen * UnsafeUtility.SizeOf<T>()), data->m_Data, data->m_NextWritePos * UnsafeUtility.SizeOf<T>());				
				
				data->m_NextWritePos = data->m_NextWritePos + data->m_Capacity - data->m_NextReadPos;
				data->m_NextReadPos = 0;
			}
			else
			{
				int dataLen = data->m_NextWritePos - data->m_NextReadPos;
				UnsafeUtility.MemCpy(newData, (IntPtr)((Byte*)data->m_Data + data->m_NextReadPos * UnsafeUtility.SizeOf<T>()), dataLen * UnsafeUtility.SizeOf<T>());				
				data->m_NextWritePos -= data->m_NextReadPos;
				data->m_NextReadPos = 0;
			}

			UnsafeUtility.Free(data->m_Data, label);
			data->m_Data = newData;
			data->m_Capacity = newCapacity;
		}
		public unsafe static void DeallocateQueue(IntPtr buffer, Allocator allocation)
		{
			QueueData* data = (QueueData*)buffer;
			UnsafeUtility.Free(data->m_Data, allocation);
			data->m_Data = IntPtr.Zero;
			UnsafeUtility.Free(buffer, allocation);
		}
	}
	[StructLayout (LayoutKind.Sequential)]
	[NativeContainer]
	public struct NativeQueue<T> where T : struct
	{

		System.IntPtr 			m_Buffer;

		#if ENABLE_NATIVE_ARRAY_CHECKS
		AtomicSafetyHandle 		m_Safety;
		DisposeSentinel			m_DisposeSentinel;
		#endif

		Allocator 				m_AllocatorLabel;

		public unsafe NativeQueue(int capacity, Allocator label)
		{
			m_AllocatorLabel = label;

			QueueData.AllocateQueue<T>(capacity+1, label, out m_Buffer);

			#if ENABLE_NATIVE_ARRAY_CHECKS
			DisposeSentinel.Create(m_Buffer, label, out m_Safety, out m_DisposeSentinel, 0, QueueData.DeallocateQueue);
			#endif
		}

		unsafe public int Count
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				QueueData* data = (QueueData*)m_Buffer;
				int writePos = data->m_NextWritePos;
				if (writePos < data->m_NextReadPos)
					writePos += data->m_Capacity;
				return writePos - data->m_NextReadPos;
			}
		}
		unsafe public int Capacity
		{
			get
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				#endif

				QueueData* data = (QueueData*)m_Buffer;
				return data->m_Capacity-1;
			}
			set
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif

				QueueData* data = (QueueData*)m_Buffer;
				QueueData.ReallocateQueue<T>(data, value+1, m_AllocatorLabel);
			}
		}

		unsafe public T Peek()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			#endif
			QueueData* data = (QueueData*)m_Buffer;
			if (data->m_NextReadPos == data->m_NextWritePos)
				throw new InvalidOperationException("Trying to peek in an empty queue");
			int idx = data->m_NextReadPos;
			return UnsafeUtility.ReadArrayElement<T>(data->m_Data, idx);
		}

		public static int GrowCapacity(int capacity)
		{
			if (capacity == 0)
				return 1;
			return capacity * 2;
		}
		unsafe public void Enqueue(T entry)
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			QueueData* data = (QueueData*)m_Buffer;
			int newWritePos = (data->m_NextWritePos+1) % data->m_Capacity;
			if (newWritePos == data->m_NextReadPos)
			{
				Capacity = GrowCapacity(Capacity);
				newWritePos = (data->m_NextWritePos+1) % data->m_Capacity;
			}
			int idx = data->m_NextWritePos;
			data->m_NextWritePos = newWritePos;
			UnsafeUtility.WriteArrayElement(data->m_Data, idx, entry);
		}

		unsafe public T Dequeue()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			QueueData* data = (QueueData*)m_Buffer;
			if (data->m_NextReadPos == data->m_NextWritePos)
				throw new InvalidOperationException("Trying to dequeue from an empty queue");
			int idx = data->m_NextReadPos;
			data->m_NextReadPos = (data->m_NextReadPos + 1) % data->m_Capacity;
			return UnsafeUtility.ReadArrayElement<T>(data->m_Data, idx);
		}

		unsafe public void Clear()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
			#endif
			QueueData* data = (QueueData*)m_Buffer;
			data->m_NextWritePos = 0;
			data->m_NextReadPos = 0;
		}

		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		public void Dispose()
		{
			#if ENABLE_NATIVE_ARRAY_CHECKS            
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
			#endif

			QueueData.DeallocateQueue(m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}
		[NativeContainer]
		[NativeContainerIsAtomicWriteOnly]
		public struct Concurrent
		{
			IntPtr 	m_Buffer;

			#if ENABLE_NATIVE_ARRAY_CHECKS
			AtomicSafetyHandle m_Safety;
			#endif

			unsafe public static implicit operator NativeQueue<T>.Concurrent (NativeQueue<T> queue)
			{
				NativeQueue<T>.Concurrent concurrent;
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(queue.m_Safety);
				concurrent.m_Safety = queue.m_Safety;
				AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
				#endif

				concurrent.m_Buffer = queue.m_Buffer;
				return concurrent;
			}

			unsafe public int Capacity
			{
				get
				{
					#if ENABLE_NATIVE_ARRAY_CHECKS
					AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
					#endif

					QueueData* data = (QueueData*)m_Buffer;
					return data->m_Capacity;
				}
			}
			unsafe public void Enqueue(T entry)
			{
				#if ENABLE_NATIVE_ARRAY_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
				#endif
				QueueData* data = (QueueData*)m_Buffer;
				int idx, newWritePos;
				do
				{
					idx = data->m_NextWritePos;
					newWritePos = (idx+1) % data->m_Capacity;
					if (newWritePos == data->m_NextReadPos)
						throw new InvalidOperationException("Queue full");
				} while (Interlocked.CompareExchange(ref data->m_NextWritePos, newWritePos, idx) != idx);
				UnsafeUtility.WriteArrayElement(data->m_Data, idx, entry);
			}
		}
	}
}