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
            
            CachedBeginIndex = m_CurrentChunkIndex + m_CurrentArchetypeIndex;
            CachedEndIndex = CachedBeginIndex + m_CurrentChunk->count;
            CachedSizeOf = archetype->sizeOfs[typeIndexInArchetype];
            CachedPtr = m_CurrentChunk->buffer + archetype->offsets[typeIndexInArchetype] - CachedBeginIndex * CachedSizeOf;
        }
    }
}