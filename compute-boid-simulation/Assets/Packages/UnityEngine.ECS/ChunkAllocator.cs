using System;
using UnityEngine.Collections;

namespace UnityEngine.ECS
{
	unsafe struct ChunkAllocator : IDisposable
	{
		byte* m_FirstChunk;
		byte* m_LastChunk;
		int m_LastChunkUsedSize;
		const int ms_ChunkSize = 64 * 1024;
		const int ms_ChunkAlignment = 64;
		
        public void Dispose()
		{
			while (m_FirstChunk != null)
			{
				byte* nextChunk = ((byte**)m_FirstChunk)[0];
				UnsafeUtility.Free((IntPtr)m_FirstChunk, Allocator.Persistent);
				m_FirstChunk = nextChunk;
			}
			m_LastChunk = null;

		}

		public IntPtr Allocate(int size, int alignment)
		{
			int alignedChunkSize = (m_LastChunkUsedSize+alignment-1) & ~(alignment-1);
			if (m_LastChunk == null || size > ms_ChunkSize - alignedChunkSize)
			{
				// Allocate new chunk
				byte* newChunk = (byte*)UnsafeUtility.Malloc (ms_ChunkSize, ms_ChunkAlignment, Allocator.Persistent);
				((byte**)newChunk)[0] = null;
				if (m_LastChunk != null)
					((byte**)m_LastChunk)[0] = newChunk;
				else
					m_FirstChunk = newChunk;
				m_LastChunk = newChunk;
				m_LastChunkUsedSize = sizeof(byte*);
				alignedChunkSize = (m_LastChunkUsedSize+alignment-1) & ~(alignment-1);
			}
			byte* ptr = m_LastChunk + alignedChunkSize;
			m_LastChunkUsedSize = alignedChunkSize+size;
			return (IntPtr)ptr;
		}

        public IntPtr Construct(int size, int alignment, void* src)
        {
            IntPtr res = Allocate(size, alignment);
            UnsafeUtility.MemCpy(res, (IntPtr)src, size);
            return res;
        }

    }
}