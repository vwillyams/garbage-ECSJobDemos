using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace UnityEngine.ECS
{
    unsafe struct ComponentDataArchetypeSegment
    {
        public Archetype*                     archetype;
        public int                            typeIndexInArchetype;
        public ComponentDataArchetypeSegment* nextSegment;
    }

    unsafe struct ComponentChunkCache
    {
        [NativeDisableUnsafePtrRestriction]
        public void*                           CachedPtr;
        public int                             CachedBeginIndex;
        public int                             CachedEndIndex;
        public int                             CachedSizeOf;
    }

    unsafe struct ComponentChunkIterator
    {
        [NativeDisableUnsafePtrRestriction]
        MatchingArchetypes*          m_FirstMatchingArchetype;
        [NativeDisableUnsafePtrRestriction]
        MatchingArchetypes*          m_CurrentMatchingArchetype;
        int                                     m_ComponentIndex;
        int                                     m_CurrentArchetypeIndex;
        [NativeDisableUnsafePtrRestriction]
        Chunk*                                  m_CurrentChunk;
        int                                     m_CurrentChunkIndex;


        [NativeDisableUnsafePtrRestriction]
        int*                                    m_filteredSharedComponents;

        //TODO: this function is identical to ComponentGroup.ChunkMatchesFilter
        bool ChunkMatchesFilter(Archetype* archeType, Chunk* chunk, ComponentDataArchetypeSegment* segement)
        {
            int* sharedComponentsInChunk = ArchetypeManager.GetSharedComponentValueArray(chunk);
            int filteredCount = m_filteredSharedComponents[0];
            var filtered = m_filteredSharedComponents + 1;
            for(int i=0; i<filteredCount; ++i)
            {
                int componetIndexInComponentGroup = filtered[i * 2];
                int sharedComponentIndex = filtered[i * 2 + 1];
                int componentIndexInArcheType = segement->typeIndexInArchetype;
                int componentIndexInChunk = archeType->sharedComponentOffset[componentIndexInArcheType];
                if (sharedComponentsInChunk[componentIndexInChunk] != sharedComponentIndex)
                    return false;
            }

            return true;
        }

//        private Chunk* GetNextMatchingChunk()
//        {
//
//        }

        //MatchingArchetypes.GetTypeIndexInArchetypeArray
        public ComponentChunkIterator(MatchingArchetypes* match, int componentIndex, int length, Chunk* firstChunk, int* filteredSharedComponents)
        {
            m_FirstMatchingArchetype = match;
            m_CurrentMatchingArchetype = match;
            m_ComponentIndex = componentIndex;
            m_CurrentArchetypeIndex = 0;
            m_CurrentChunk = firstChunk;
            m_CurrentChunkIndex = 0;
            m_filteredSharedComponents = filteredSharedComponents;
        }

        public object GetManagedObject(ArchetypeManager typeMan, int typeIndexInArchetype, int cachedBeginIndex, int index)
        {
            return typeMan.GetManagedObject(m_CurrentChunk, typeIndexInArchetype, index - cachedBeginIndex);
        }

        public object GetManagedObject(ArchetypeManager typeMan, int cachedBeginIndex, int index)
        {
            return typeMan.GetManagedObject(m_CurrentChunk, MatchingArchetypes.GetTypeIndexInArchetypeArray(m_CurrentMatchingArchetype)[m_ComponentIndex], index - cachedBeginIndex);
        }

        public object[] GetManagedObjectRange(ArchetypeManager typeMan, int cachedBeginIndex, int index, out int rangeStart, out int rangeLength)
        {
            var objs = typeMan.GetManagedObjectRange(m_CurrentChunk, MatchingArchetypes.GetTypeIndexInArchetypeArray(m_CurrentMatchingArchetype)[m_ComponentIndex], out rangeStart, out rangeLength);
            rangeStart += index - cachedBeginIndex;
            rangeLength -= index - cachedBeginIndex;
            return objs;
        }

        public void UpdateCache(int index, out ComponentChunkCache cache)
        {
            //if (m_filteredSharedComponents == null)
            {
                if (index < m_CurrentArchetypeIndex)
                {
                    m_CurrentMatchingArchetype = m_FirstMatchingArchetype;
                    m_CurrentArchetypeIndex = 0;
                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->chunkList.Begin();
                    m_CurrentChunkIndex = 0;
                }

                while (index >= m_CurrentArchetypeIndex + m_CurrentMatchingArchetype->archetype->entityCount)
                {
                    m_CurrentArchetypeIndex += m_CurrentMatchingArchetype->archetype->entityCount;
                    m_CurrentMatchingArchetype = m_CurrentMatchingArchetype->next;
                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->chunkList.Begin();
                    m_CurrentChunkIndex = 0;
                }

                index -= m_CurrentArchetypeIndex;
                if (index < m_CurrentChunkIndex)
                {
                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->chunkList.Begin();
                    m_CurrentChunkIndex = 0;
                }

                while (index >= m_CurrentChunkIndex + m_CurrentChunk->count)
                {
                    m_CurrentChunkIndex += m_CurrentChunk->count;
                    m_CurrentChunk = (Chunk*) m_CurrentChunk->chunkListNode.next;
                }
            }
//            else
//            {
//                int currentIndex = 0;
//                //TODO: need the full
//                m_CurrentArchetypeSegment = m_FirstArchetypeSegment;
//                m_CurrentArchetypeIndex = 0;
//                while (true)
//                {
//                    Archetype* archeType = m_CurrentArchetypeSegment->archetype;
//                    for (Chunk* c = (Chunk*)archeType->chunkList.Begin(); c != archeType->chunkList.End(); c = (Chunk*)c->chunkListNode.next)
//                    {
//                        if (ChunkMatchesFilter(archeType, c, m_CurrentArchetypeSegment))
//                        {
//                            currentIndex += c->count;
//                        }
//                    }
//                    m_CurrentArchetypeSegment = m_CurrentArchetypeSegment->nextSegment;
//                }
//            }

            var archetype = m_CurrentMatchingArchetype->archetype;
            var typeIndexInArchetype =MatchingArchetypes.GetTypeIndexInArchetypeArray(m_CurrentMatchingArchetype)[m_ComponentIndex];

            cache.CachedBeginIndex = m_CurrentChunkIndex + m_CurrentArchetypeIndex;
            cache.CachedEndIndex = cache.CachedBeginIndex + m_CurrentChunk->count;
            cache.CachedSizeOf = archetype->sizeOfs[typeIndexInArchetype];
            cache.CachedPtr = m_CurrentChunk->buffer + archetype->offsets[typeIndexInArchetype] - cache.CachedBeginIndex * cache.CachedSizeOf;
        }

        public void GetCacheForType(int componentType, out ComponentChunkCache cache, out int typeIndexInArchetype)
        {
            var archetype = m_CurrentMatchingArchetype->archetype;

            typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(archetype, componentType);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (typeIndexInArchetype == -1)
                throw new System.ArgumentException("componentType does not exist in the iterated archetype");
            #endif

            cache.CachedBeginIndex = m_CurrentChunkIndex + m_CurrentArchetypeIndex;
            cache.CachedEndIndex = cache.CachedBeginIndex + m_CurrentChunk->count;
            cache.CachedSizeOf = archetype->sizeOfs[typeIndexInArchetype];
            cache.CachedPtr = m_CurrentChunk->buffer + archetype->offsets[typeIndexInArchetype] - cache.CachedBeginIndex * cache.CachedSizeOf;
        }
    }
}
