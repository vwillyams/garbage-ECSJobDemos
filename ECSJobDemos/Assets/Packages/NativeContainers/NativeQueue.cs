using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using System.Threading;

namespace Unity.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeQueueData
    {
        public System.IntPtr m_Data;
        public int m_NextFreeBlock;
        public int m_FirstUsedBlock;

        public int m_Capacity;

        public int m_BlockSize;
        public int m_NumBlocks;

        public int m_CurrentReadIndexInBlock;

        public const int IntsPerCacheLine = JobsUtility.CacheLineSize / sizeof(int);

        public fixed int m_CurrentWriteBlockTLS[JobsUtility.MaxJobThreadCount * IntsPerCacheLine];
        public fixed int m_FreeCountTLS[JobsUtility.MaxJobThreadCount * IntsPerCacheLine];
		public int m_FreeCount;

        public static int* GetBlockLengths<T>(NativeQueueData* data) where T : struct
        {
            return (int*)(((Byte*)data->m_Data) + data->m_BlockSize * UnsafeUtility.SizeOf<T>() * data->m_NumBlocks);
        }

		public static bool ReduceFreeCountMT(NativeQueueData* data, int threadIndex)
		{
			if (data->m_FreeCountTLS[threadIndex * IntsPerCacheLine] > 0)
			{
				int count = Interlocked.Decrement(ref data->m_FreeCountTLS[threadIndex * IntsPerCacheLine]);
				if (count >= 0)
					return true;
			}
			if (data->m_FreeCount > 0)
			{
				data->m_FreeCountTLS[threadIndex * IntsPerCacheLine] = -0xffff;
				// Grab some data from the shared cache
				int count = Interlocked.Add(ref data->m_FreeCount, -16) + 16;
				count = Math.Min(16, count);
				if (count > 0)
				{
					data->m_FreeCountTLS[threadIndex * IntsPerCacheLine] = count-1;
					return true;
				}
			}
			// Try to steal a single item from another worker
			bool again = true;
			while (again)
			{
				again = false;
				for (int other = (threadIndex+1) % JobsUtility.MaxJobThreadCount; other != threadIndex; other = (other+1) % JobsUtility.MaxJobThreadCount)
				{
					while (true)
					{
						int otherCount = data->m_FreeCountTLS[other * IntsPerCacheLine];
						if (otherCount < 0)
						{
							if (otherCount == -0xffff)
								again = true;
							break;
						}
						if (Interlocked.CompareExchange(ref data->m_FreeCountTLS[other * IntsPerCacheLine], otherCount - 1, otherCount) == otherCount)
							return true;
					}
				}
			}
			return false;
		}
		public static bool ReduceFreeCountST(NativeQueueData* data)
		{
			if (data->m_FreeCountTLS[0] > 0)
			{
				--data->m_FreeCountTLS[0];
				return true;
			}
			if (data->m_FreeCount > 0)
			{
				// Grab some data from the shared cache
				int count = Math.Min(16, data->m_FreeCount);
				data->m_FreeCount -= count;
				data->m_FreeCountTLS[0] += count - 1;
				return true;
			}
			// Try to steal a single item from another worker
			for (int other = 1 % JobsUtility.MaxJobThreadCount; other != 0; other = (other+1) % JobsUtility.MaxJobThreadCount)
			{
				if (data->m_FreeCountTLS[other * IntsPerCacheLine] > 0)
				{
					--data->m_FreeCountTLS[other * IntsPerCacheLine];
					return true;
				}
			}
			return false;
		}

        public static int AllocateWriteBlockMT<T>(NativeQueueData* data, int threadIndex) where T : struct
        {
            int tlsIdx = threadIndex;

            int* blockLengths = GetBlockLengths<T>(data);
            int currentWriteBlock = data->m_CurrentWriteBlockTLS[tlsIdx * IntsPerCacheLine];
            if (currentWriteBlock >= 0 && blockLengths[currentWriteBlock * IntsPerCacheLine] == data->m_BlockSize)
                currentWriteBlock = -1;
            if (currentWriteBlock < 0)
            {
                int nextFreeBlock;
                do
                {
                    currentWriteBlock = data->m_NextFreeBlock;
                    if (currentWriteBlock == data->m_FirstUsedBlock && blockLengths[data->m_FirstUsedBlock * IntsPerCacheLine] > 0)
                        throw new InvalidOperationException("Queue full");
                    nextFreeBlock = (currentWriteBlock + 1) % data->m_NumBlocks;
                } while (Interlocked.CompareExchange(ref data->m_NextFreeBlock, nextFreeBlock, currentWriteBlock) != currentWriteBlock);
                blockLengths[currentWriteBlock * IntsPerCacheLine] = 0;
                data->m_CurrentWriteBlockTLS[tlsIdx * IntsPerCacheLine] = currentWriteBlock;
            }
            return currentWriteBlock;
        }

        public unsafe static void AllocateQueue<T>(int capacity, Allocator label, out IntPtr outBuf) where T : struct
        {
            outBuf = UnsafeUtility.Malloc((ulong)UnsafeUtility.SizeOf<NativeQueueData>(), UnsafeUtility.AlignOf<NativeQueueData>(), label);
            NativeQueueData* data = (NativeQueueData*)outBuf;
            data->m_NextFreeBlock = 0;
            data->m_FirstUsedBlock = 0;
            data->m_CurrentReadIndexInBlock = 0;
			data->m_FreeCount = capacity;
            for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
			{
                data->m_CurrentWriteBlockTLS[tls * IntsPerCacheLine] = -1;
                data->m_FreeCountTLS[tls * IntsPerCacheLine] = 0;
			}

            data->m_BlockSize = (JobsUtility.CacheLineSize + UnsafeUtility.SizeOf<T>() - 1) / UnsafeUtility.SizeOf<T>();
            // Round up the capacity to be an even number of blocks, add the number of threads as extra overhead since threads can allocate only one item from the block
            data->m_NumBlocks = (capacity + data->m_BlockSize - 1) / data->m_BlockSize + JobsUtility.MaxJobThreadCount;
            // The circular buffer requires at least 2 blocks to work
            if (data->m_NumBlocks < 2)
                data->m_NumBlocks = 2;

            data->m_Capacity = capacity;

            int totalSizeInBytes = (64 + data->m_BlockSize * UnsafeUtility.SizeOf<T>()) * data->m_NumBlocks;

            data->m_Data = UnsafeUtility.Malloc((ulong)totalSizeInBytes, JobsUtility.CacheLineSize, label);
            int* blockLengths = GetBlockLengths<T>(data);
            for (int i = 0; i < data->m_NumBlocks; ++i)
                blockLengths[i * IntsPerCacheLine] = 0;
        }
        public unsafe static void ReallocateQueue<T>(NativeQueueData* data, int newCapacity, Allocator label) where T : struct
        {
            if (data->m_Capacity == newCapacity)
                return;

            if (data->m_Capacity > newCapacity)
                throw new System.Exception("Shrinking a queue is not supported");

			data->m_FreeCount += newCapacity - data->m_Capacity;

            int newBlockSize = (JobsUtility.CacheLineSize + UnsafeUtility.SizeOf<T>() - 1) / UnsafeUtility.SizeOf<T>();
            int newNumBlocks = (newCapacity + newBlockSize - 1) / newBlockSize + JobsUtility.MaxJobThreadCount;
            if (newNumBlocks < 2)
                newNumBlocks = 2;

            if (data->m_BlockSize == newBlockSize && data->m_NumBlocks == newNumBlocks)
            {
                data->m_Capacity = newCapacity;
                return;
            }

            int totalSizeInBytes = (64 + newBlockSize * UnsafeUtility.SizeOf<T>()) * newNumBlocks;
            IntPtr newData = UnsafeUtility.Malloc((ulong)totalSizeInBytes, JobsUtility.CacheLineSize, label);

            // Copy the data from the old buffer to the new while compacting it and tracking the size
            int count = 0;
            int* blockLengths = GetBlockLengths<T>(data);
            int i = data->m_FirstUsedBlock;
            Byte* dstPtr = (Byte*)newData;
            if (blockLengths[i * IntsPerCacheLine] != 0)
            {
                Byte* srcPtr = ((Byte*)data->m_Data) + (i * data->m_BlockSize + data->m_CurrentReadIndexInBlock) * UnsafeUtility.SizeOf<T>();
                count = blockLengths[i * IntsPerCacheLine] - data->m_CurrentReadIndexInBlock;
                UnsafeUtility.MemCpy((IntPtr)dstPtr, (IntPtr)srcPtr, (ulong)(count * UnsafeUtility.SizeOf<T>()));
                i = (i + 1) % data->m_NumBlocks;
                while (i != data->m_NextFreeBlock)
                {
                    srcPtr = ((Byte*)data->m_Data) + i * data->m_BlockSize * UnsafeUtility.SizeOf<T>();
                    UnsafeUtility.MemCpy((IntPtr)(dstPtr + count), (IntPtr)srcPtr, (ulong)(blockLengths[i * IntsPerCacheLine] * UnsafeUtility.SizeOf<T>()));
                    count += blockLengths[i * IntsPerCacheLine];
                    i = (i + 1) % data->m_NumBlocks;
                }
            }
            for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
			{
                data->m_CurrentWriteBlockTLS[tls * IntsPerCacheLine] = -1;
				if (data->m_FreeCountTLS[tls * IntsPerCacheLine] > 0)
					data->m_FreeCount += data->m_FreeCountTLS[tls * IntsPerCacheLine];
                data->m_FreeCountTLS[tls * IntsPerCacheLine] = 0;
			}

            UnsafeUtility.Free(data->m_Data, label);
            data->m_Data = newData;
            data->m_BlockSize = newBlockSize;
            data->m_NumBlocks = newNumBlocks;
            data->m_Capacity = newCapacity;
            data->m_FirstUsedBlock = 0;
            data->m_CurrentReadIndexInBlock = 0;
            data->m_NextFreeBlock = count / newBlockSize;
            if (count % newBlockSize > 0)
            {
                data->m_CurrentWriteBlockTLS[0] = data->m_NextFreeBlock;
                data->m_NextFreeBlock = (data->m_NextFreeBlock + 1) % newNumBlocks;
            }
            blockLengths = GetBlockLengths<T>(data);
            for (int block = 0; block < newNumBlocks; ++block)
            {
                int len = Math.Min(count, data->m_BlockSize);
                blockLengths[block * IntsPerCacheLine] = len;
                count -= len;
            }
        }
        public unsafe static void DeallocateQueue(IntPtr buffer, Allocator allocation)
        {
            NativeQueueData* data = (NativeQueueData*)buffer;
            UnsafeUtility.Free(data->m_Data, allocation);
            data->m_Data = IntPtr.Zero;
            UnsafeUtility.Free(buffer, allocation);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    public struct NativeQueue<T> where T : struct
    {

        System.IntPtr m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;
        DisposeSentinel m_DisposeSentinel;
#endif

        Allocator m_AllocatorLabel;

        public unsafe NativeQueue(int capacity, Allocator label)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS            
            if (!UnsafeUtility.IsBlittable<T>())
                throw new ArgumentException(string.Format("{0} used in NativeQueue<{0}> must be blittable", typeof(T)));
#endif
            m_AllocatorLabel = label;

			NativeQueueData.AllocateQueue<T>(capacity, label, out m_Buffer);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Create(m_Buffer, label, out m_Safety, out m_DisposeSentinel, 0, NativeQueueData.DeallocateQueue);
#endif
		}

		unsafe public int Count
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

				NativeQueueData* data = (NativeQueueData*)m_Buffer;
				int count = data->m_Capacity - data->m_FreeCount;
				for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
				{
					if (data->m_FreeCountTLS[tls * NativeQueueData.IntsPerCacheLine] > 0)
						count -= data->m_FreeCountTLS[tls * NativeQueueData.IntsPerCacheLine];
				}
				return count;
			}
		}
		unsafe public int Capacity
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

				NativeQueueData* data = (NativeQueueData*)m_Buffer;
				return data->m_Capacity;
			}
			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

				NativeQueueData* data = (NativeQueueData*)m_Buffer;
				NativeQueueData.ReallocateQueue<T>(data, value, m_AllocatorLabel);
			}
		}

		unsafe public T Peek()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

			NativeQueueData* data = (NativeQueueData*)m_Buffer;
			int* blockLengths = NativeQueueData.GetBlockLengths<T>(data);

			int firstUsedBlock = data->m_FirstUsedBlock;
			int readIndexInBlock = data->m_CurrentReadIndexInBlock;
			if (data->m_CurrentReadIndexInBlock >= blockLengths[firstUsedBlock*NativeQueueData.IntsPerCacheLine] && blockLengths[firstUsedBlock*NativeQueueData.IntsPerCacheLine] > 0)
			{
				int nextUsedBlock = (firstUsedBlock+1) % data->m_NumBlocks;
				if (blockLengths[nextUsedBlock*NativeQueueData.IntsPerCacheLine] == 0 && blockLengths[firstUsedBlock*NativeQueueData.IntsPerCacheLine] != data->m_BlockSize)
					throw new InvalidOperationException("Trying to peek from an empty queue");
				firstUsedBlock = nextUsedBlock;
				readIndexInBlock = 0;
			}
			if (blockLengths[firstUsedBlock*NativeQueueData.IntsPerCacheLine] == 0)
				throw new InvalidOperationException("Trying to peek from an empty queue");

			int idx = firstUsedBlock * data->m_BlockSize + readIndexInBlock;
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

			NativeQueueData* data = (NativeQueueData*)m_Buffer;
			bool isFull = (data->m_FreeCount == 0);
			for (int tls = 0; tls < JobsUtility.MaxJobThreadCount && isFull; ++tls)
				isFull = (data->m_FreeCountTLS[tls * NativeQueueData.IntsPerCacheLine] == 0);
			if (isFull)
				Capacity = GrowCapacity(Capacity);

            int* blockLengths = NativeQueueData.GetBlockLengths<T>(data);
			if (data->m_CurrentWriteBlockTLS[0] < 0)
			{
				while (data->m_NextFreeBlock == data->m_FirstUsedBlock && blockLengths[data->m_FirstUsedBlock*NativeQueueData.IntsPerCacheLine] > 0)
				{
					Capacity = GrowCapacity(Capacity);
				}
				data->m_CurrentWriteBlockTLS[0] = data->m_NextFreeBlock;
				blockLengths[data->m_CurrentWriteBlockTLS[0]*NativeQueueData.IntsPerCacheLine] = 0;
				data->m_NextFreeBlock = (data->m_NextFreeBlock + 1) % data->m_NumBlocks;
			}

			// Reduce free count by 1
			if (!NativeQueueData.ReduceFreeCountST(data))
			{
				Capacity = GrowCapacity(Capacity);
				Enqueue(entry);
				return;
			}

			int idx = data->m_CurrentWriteBlockTLS[0] * data->m_BlockSize + blockLengths[data->m_CurrentWriteBlockTLS[0]*NativeQueueData.IntsPerCacheLine];
			UnsafeUtility.WriteArrayElement(data->m_Data, idx, entry);
			++blockLengths[data->m_CurrentWriteBlockTLS[0]*NativeQueueData.IntsPerCacheLine];
			if (blockLengths[data->m_CurrentWriteBlockTLS[0]*NativeQueueData.IntsPerCacheLine] == data->m_BlockSize)
				data->m_CurrentWriteBlockTLS[0] = -1;
		}

		unsafe public T Dequeue()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			NativeQueueData* data = (NativeQueueData*)m_Buffer;
			int* blockLengths = NativeQueueData.GetBlockLengths<T>(data);

			if (data->m_CurrentReadIndexInBlock >= blockLengths[data->m_FirstUsedBlock*NativeQueueData.IntsPerCacheLine] && blockLengths[data->m_FirstUsedBlock*NativeQueueData.IntsPerCacheLine] > 0)
			{
				int nextUsedBlock = (data->m_FirstUsedBlock+1) % data->m_NumBlocks;
				if (blockLengths[nextUsedBlock*NativeQueueData.IntsPerCacheLine] == 0 && blockLengths[data->m_FirstUsedBlock*NativeQueueData.IntsPerCacheLine] != data->m_BlockSize)
					throw new InvalidOperationException("Trying to dequeue from an empty queue");
				blockLengths[data->m_FirstUsedBlock*NativeQueueData.IntsPerCacheLine] = 0;
				for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
				{
					if (data->m_CurrentWriteBlockTLS[tls*NativeQueueData.IntsPerCacheLine] == data->m_FirstUsedBlock)
						data->m_CurrentWriteBlockTLS[tls*NativeQueueData.IntsPerCacheLine] = -1;
				}
				if (data->m_CurrentWriteBlockTLS[0] == data->m_FirstUsedBlock)
					data->m_CurrentWriteBlockTLS[0] = -1;
				data->m_FirstUsedBlock = nextUsedBlock;
				data->m_CurrentReadIndexInBlock = 0;
			}
			if (blockLengths[data->m_FirstUsedBlock*NativeQueueData.IntsPerCacheLine] == 0)
				throw new InvalidOperationException("Trying to dequeue from an empty queue");

			++data->m_FreeCount;

            int idx = data->m_FirstUsedBlock * data->m_BlockSize + data->m_CurrentReadIndexInBlock;
			data->m_CurrentReadIndexInBlock++;
			return UnsafeUtility.ReadArrayElement<T>(data->m_Data, idx);
		}

		unsafe public void Clear()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			NativeQueueData* data = (NativeQueueData*)m_Buffer;
			data->m_NextFreeBlock = 0;
			data->m_FirstUsedBlock = 0;
			data->m_CurrentReadIndexInBlock = 0;
			data->m_FreeCount = data->m_Capacity;

            for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
			{
                data->m_CurrentWriteBlockTLS[tls * NativeQueueData.IntsPerCacheLine] = -1;
				data->m_FreeCountTLS[tls * NativeQueueData.IntsPerCacheLine] = 0;
			}

            int* blockLengths = NativeQueueData.GetBlockLengths<T>(data);
			for (int i = 0 ; i < data->m_NumBlocks; ++i)
				blockLengths[i*NativeQueueData.IntsPerCacheLine] = 0;
		}

		public bool IsCreated
		{
			get { return m_Buffer != IntPtr.Zero; }
		}

		public void Dispose()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif

			NativeQueueData.DeallocateQueue(m_Buffer, m_AllocatorLabel);
			m_Buffer = IntPtr.Zero;
		}
		[NativeContainer]
		[NativeContainerIsAtomicWriteOnly]
		[NativeContainerNeedsThreadIndex]
		public struct Concurrent
		{
			IntPtr 	m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle m_Safety;
#endif

			int m_ThreadIndex;

			unsafe public static implicit operator NativeQueue<T>.Concurrent (NativeQueue<T> queue)
			{
				NativeQueue<T>.Concurrent concurrent;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(queue.m_Safety);
				concurrent.m_Safety = queue.m_Safety;
				AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
#endif

				concurrent.m_Buffer = queue.m_Buffer;
				concurrent.m_ThreadIndex = 0;
				return concurrent;
			}

			unsafe public int Capacity
			{
				get
				{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

					NativeQueueData* data = (NativeQueueData*)m_Buffer;
					return data->m_Capacity;
				}
			}
			unsafe public void Enqueue(T entry)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
				NativeQueueData* data = (NativeQueueData*)m_Buffer;

				if (!NativeQueueData.ReduceFreeCountMT(data, m_ThreadIndex))
					throw new InvalidOperationException("Queue full");
				int* blockLengths = NativeQueueData.GetBlockLengths<T>(data);
				int writeBlock = NativeQueueData.AllocateWriteBlockMT<T>(data, m_ThreadIndex);
				int idx = writeBlock * data->m_BlockSize + blockLengths[writeBlock*NativeQueueData.IntsPerCacheLine];
				UnsafeUtility.WriteArrayElement(data->m_Data, idx, entry);
				++blockLengths[writeBlock*NativeQueueData.IntsPerCacheLine];
			}
		}
	}
}