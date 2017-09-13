using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;
namespace UnityEngine.ECS
{
    public unsafe struct ComponentDataArchetypeSegment
    {
        public Archetype* archetype;
        public int offset;
        public int stride;
        public int typeIndex;
        public ComponentDataArchetypeSegment* nextSegment;
    }

    unsafe struct ComponentDataArrayCache
    {
        public IntPtr                          m_CachedPtr;
        public int                             m_CachedStride;
        public int                             m_CachedBeginIndex;
        public int                             m_CachedEndIndex;

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
            if (length > 0)
                m_CurrentChunk = data->archetype->first;
            else
                m_CurrentChunk = null;
            m_CurrentChunkIndex = 0;

            m_CachedPtr = IntPtr.Zero;
            m_CachedStride = 0;
            m_CachedBeginIndex = 0;
            m_CachedEndIndex = 0;
        }

        public object GetManagedObject(TypeManager typeMan, int index)
        {
            return typeMan.GetManagedObject(m_CurrentChunk, m_CurrentArchetypeSegment->typeIndex, index - m_CachedBeginIndex);
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

            m_CachedPtr = m_CurrentChunk->buffer + m_CurrentArchetypeSegment->offset - (m_CurrentArchetypeIndex + m_CurrentChunkIndex) * m_CurrentArchetypeSegment->stride;
            m_CachedStride = m_CurrentArchetypeSegment->stride;
            m_CachedBeginIndex = m_CurrentChunkIndex;
            m_CachedEndIndex = m_CurrentChunkIndex + m_CurrentChunk->count;
        }
    }
}