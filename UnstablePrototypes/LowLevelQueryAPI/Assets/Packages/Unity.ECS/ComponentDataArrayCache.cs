using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;
namespace UnityEngine.ECS
{
    unsafe struct ComponentDataArchetypeSegment
    {
        public Archetype*                     archetype;
        public int                            typeIndexInArchetype;
        public ComponentDataArchetypeSegment* nextSegment;
    }

    unsafe struct ComponentDataArrayCache
    {
        public IntPtr                          CachedPtr;
        public int                             CachedStride;
        public int                             CachedBeginIndex;
        public int                             CachedEndIndex;
        public int                             CachedSizeOf;

        ComponentDataArchetypeSegment*  m_FirstArchetypeSegment;
        ComponentDataArchetypeSegment*  m_CurrentArchetypeSegment;
        int                             m_CurrentArchetypeIndex;
        Chunk*                          m_CurrentChunk;
        int                             m_CurrentChunkIndex;

        public ComponentDataArrayCache(ComponentDataArchetypeSegment* data, int length)
        {
            m_FirstArchetypeSegment = data;
            m_CurrentArchetypeSegment = data;
            m_CurrentArchetypeIndex = 0;
            CachedSizeOf = 0;
            if (length > 0)
                m_CurrentChunk = data->archetype->first;
            else
                m_CurrentChunk = null;
            m_CurrentChunkIndex = 0;

            CachedPtr = IntPtr.Zero;
            CachedStride = 0;
            CachedBeginIndex = 0;
            CachedEndIndex = 0;
        }

        public object GetManagedObject(ArchetypeManager typeMan, int index)
        {
            return typeMan.GetManagedObject(m_CurrentChunk, m_CurrentArchetypeSegment->typeIndexInArchetype, index - CachedBeginIndex);
        }
        public object[] GetManagedObjectRange(ArchetypeManager typeMan, int index, out int rangeStart, out int rangeLength)
        {
            var objs = typeMan.GetManagedObjectRange(m_CurrentChunk, m_CurrentArchetypeSegment->typeIndexInArchetype, out rangeStart, out rangeLength);
            rangeStart += index - CachedBeginIndex;
            rangeLength -= index - CachedBeginIndex;
            return objs;
        }

        #if false
        public void AssertIndexOutOfBoundsInternal(int index, int size)
        {
            int indexInChunk = index - CachedBeginIndex;
            if (indexInChunk < 0)
                throw new System.InvalidOperationException(string.Format("index out of bounds index: {0} indexinchunk: {1} chunkcount:{2}", index, indexInChunk, m_CurrentChunk->count));
            if (indexInChunk >= m_CurrentChunk->count)
                throw new System.InvalidOperationException(string.Format("index out of bounds index: {0} indexinchunk: {1} chunkcount:{2}", index, indexInChunk, m_CurrentChunk->count));
            if (CachedStride != size)
                throw new System.ArgumentException("size and stride dont match");
            if (m_CurrentChunk == null)
                throw new System.ArgumentException("chunk is null");
            
            long readLocation = (long)CachedPtr + (index * CachedStride);
            long offset = readLocation - (long)m_CurrentChunk->buffer;
            if (offset < 0)
                throw new System.InvalidOperationException(string.Format("out of bounds index in index: {0} index in chunk: {1} offset: {2}", index, indexInChunk, offset));
            if (offset + size > ArchetypeManager.GetChunkBufferSize())
                throw new System.InvalidOperationException(string.Format("out of bounds index in index: {0} index in chunk: {1} offset: {2}", index, indexInChunk, offset));
        }
        #endif

        public void UpdateCache(int index)
        {
            if (index < m_CurrentArchetypeIndex)
            {
                m_CurrentArchetypeSegment = m_FirstArchetypeSegment;
                m_CurrentArchetypeIndex = 0;
                m_CurrentChunk = m_CurrentArchetypeSegment->archetype->first;
                m_CurrentChunkIndex = 0;
            }

            while (index >= m_CurrentArchetypeIndex + m_CurrentArchetypeSegment->archetype->entityCount)
            {
                m_CurrentArchetypeIndex += m_CurrentArchetypeSegment->archetype->entityCount;
                m_CurrentArchetypeSegment = m_CurrentArchetypeSegment->nextSegment;
                m_CurrentChunk = m_CurrentArchetypeSegment->archetype->first;
                m_CurrentChunkIndex = 0;
            }
            index -= m_CurrentArchetypeIndex;
            if (index < m_CurrentChunkIndex)
            {
                m_CurrentChunk = m_CurrentArchetypeSegment->archetype->first;
                m_CurrentChunkIndex = 0;
            }

            while (index >= m_CurrentChunkIndex + m_CurrentChunk->count)
            {
                m_CurrentChunkIndex += m_CurrentChunk->count;
                m_CurrentChunk = m_CurrentChunk->next;
            }

            var archetype = m_CurrentArchetypeSegment->archetype;
            var typeIndexInArchetype = m_CurrentArchetypeSegment->typeIndexInArchetype;
            
            CachedStride = archetype->strides[typeIndexInArchetype];
            CachedBeginIndex = m_CurrentChunkIndex + m_CurrentArchetypeIndex;
            CachedEndIndex = CachedBeginIndex + m_CurrentChunk->count;
            CachedPtr = m_CurrentChunk->buffer + archetype->offsets[typeIndexInArchetype] - (CachedBeginIndex * CachedStride);
            CachedSizeOf = archetype->sizeOfs[typeIndexInArchetype];
        }
    }
}