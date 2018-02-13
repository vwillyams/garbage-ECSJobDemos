using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.ECS;

namespace Unity.ECS
{
    internal unsafe struct ComponentChunkCache
    {
        [NativeDisableUnsafePtrRestriction]
        public void*                           CachedPtr;
        public int                             CachedBeginIndex;
        public int                             CachedEndIndex;
        public int                             CachedSizeOf;
    }

    internal unsafe struct ComponentChunkIterator
    {
        [NativeDisableUnsafePtrRestriction] private readonly MatchingArchetypes*                     m_FirstMatchingArchetype;
        [NativeDisableUnsafePtrRestriction] private MatchingArchetypes*                     m_CurrentMatchingArchetype;
        public int                              IndexInComponentGroup;
        private int                                     m_CurrentArchetypeIndex;
        [NativeDisableUnsafePtrRestriction] private Chunk*                                  m_CurrentChunk;
        private int                                     m_CurrentChunkIndex;


        [NativeDisableUnsafePtrRestriction]
        // The first element is the amount of filtered components
        private readonly int*                                    m_filteredSharedComponents;


        internal static bool ChunkMatchesFilter(MatchingArchetypes* match, Chunk* chunk, int* filteredSharedComponents)
        {
            var sharedComponentsInChunk = chunk->SharedComponentValueArray;
            var filteredCount = filteredSharedComponents[0];
            var filtered = filteredSharedComponents + 1;
            for(var i=0; i<filteredCount; ++i)
            {
                var componetIndexInComponentGroup = filtered[i * 2];
                var sharedComponentIndex = filtered[i * 2 + 1];
                var componentIndexInArcheType = match->typeIndexInArchetypeArray[componetIndexInComponentGroup];
                var componentIndexInChunk = match->archetype->SharedComponentOffset[componentIndexInArcheType];
                if (sharedComponentsInChunk[componentIndexInChunk] != sharedComponentIndex)
                    return false;
            }

            return true;
        }

        internal int GetSharedComponentFromCurrentChunk(int sharedComponentIndex)
        {
            var archetype = m_CurrentMatchingArchetype->archetype;
            var indexInArchetype = m_CurrentMatchingArchetype->typeIndexInArchetypeArray[sharedComponentIndex];
            var sharedComponentOffset = archetype->SharedComponentOffset[indexInArchetype];
            return m_CurrentChunk->SharedComponentValueArray[sharedComponentOffset];
        }

        private void MoveToNextMatchingChunk()
        {
            var m = m_CurrentMatchingArchetype;
            var c = m_CurrentChunk;
            var e = (Chunk*)m->archetype->ChunkList.End;

            do
            {
                c = (Chunk*)c->ChunkListNode.Next;
                while (c == e)
                {
                    m_CurrentArchetypeIndex += m_CurrentChunkIndex;
                    m_CurrentChunkIndex = 0;
                    m = m->next;
                    if (m == null)
                    {
                        m_CurrentMatchingArchetype = null;
                        m_CurrentChunk = null;
                        return;
                    }

                    c = (Chunk*)m->archetype->ChunkList.Begin;
                    e = (Chunk*)m->archetype->ChunkList.End;
                }
            } while (!(ChunkMatchesFilter(m, c, m_filteredSharedComponents) && (c->Capacity > 0)));
            m_CurrentMatchingArchetype = m;
            m_CurrentChunk = c;
        }

        public ComponentChunkIterator(MatchingArchetypes* match, int length, Chunk* firstChunk, int* filteredSharedComponents)
        {
            m_FirstMatchingArchetype = match;
            m_CurrentMatchingArchetype = match;
            IndexInComponentGroup = -1;
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
            return typeMan.GetManagedObject(m_CurrentChunk, m_CurrentMatchingArchetype->typeIndexInArchetypeArray[IndexInComponentGroup], index - cachedBeginIndex);
        }

        public object[] GetManagedObjectRange(ArchetypeManager typeMan, int cachedBeginIndex, int index, out int rangeStart, out int rangeLength)
        {
            var objs = typeMan.GetManagedObjectRange(m_CurrentChunk, m_CurrentMatchingArchetype->typeIndexInArchetypeArray[IndexInComponentGroup], out rangeStart, out rangeLength);
            rangeStart += index - cachedBeginIndex;
            rangeLength -= index - cachedBeginIndex;
            return objs;
        }

        public void UpdateCache(int index, out ComponentChunkCache cache)
        {
            Assert.IsTrue(-1 != IndexInComponentGroup);

            if (m_filteredSharedComponents == null)
            {
                if (index < m_CurrentArchetypeIndex)
                {
                    m_CurrentMatchingArchetype = m_FirstMatchingArchetype;
                    m_CurrentArchetypeIndex = 0;
                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->ChunkList.Begin;
                    m_CurrentChunkIndex = 0;
                }

                while (index >= m_CurrentArchetypeIndex + m_CurrentMatchingArchetype->archetype->EntityCount)
                {
                    m_CurrentArchetypeIndex += m_CurrentMatchingArchetype->archetype->EntityCount;
                    m_CurrentMatchingArchetype = m_CurrentMatchingArchetype->next;
                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->ChunkList.Begin;
                    m_CurrentChunkIndex = 0;
                }

                index -= m_CurrentArchetypeIndex;
                if (index < m_CurrentChunkIndex)
                {
                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->ChunkList.Begin;
                    m_CurrentChunkIndex = 0;
                }

                while (index >= m_CurrentChunkIndex + m_CurrentChunk->Count)
                {
                    m_CurrentChunkIndex += m_CurrentChunk->Count;
                    m_CurrentChunk = (Chunk*) m_CurrentChunk->ChunkListNode.Next;
                }
            }
            else
            {
                if (index < m_CurrentArchetypeIndex + m_CurrentChunkIndex)
                {
                    if (index < m_CurrentArchetypeIndex)
                    {
                        m_CurrentMatchingArchetype = m_FirstMatchingArchetype;
                        m_CurrentArchetypeIndex = 0;
                    }

                    m_CurrentChunk = (Chunk*) m_CurrentMatchingArchetype->archetype->ChunkList.Begin;
                    m_CurrentChunkIndex = 0;
                    if (!(ChunkMatchesFilter(m_CurrentMatchingArchetype, m_CurrentChunk, m_filteredSharedComponents) &&
                          (m_CurrentChunk->Count > 0)))
                    {
                        MoveToNextMatchingChunk();
                    }
                }

                while (index >= m_CurrentArchetypeIndex + m_CurrentChunkIndex + m_CurrentChunk->Count)
                {
                    m_CurrentChunkIndex += m_CurrentChunk->Count;
                    MoveToNextMatchingChunk();
                }
            }

            var archetype = m_CurrentMatchingArchetype->archetype;
            var typeIndexInArchetype = m_CurrentMatchingArchetype->typeIndexInArchetypeArray[IndexInComponentGroup];

            cache.CachedBeginIndex = m_CurrentChunkIndex + m_CurrentArchetypeIndex;
            cache.CachedEndIndex = cache.CachedBeginIndex + m_CurrentChunk->Count;
            cache.CachedSizeOf = archetype->SizeOfs[typeIndexInArchetype];
            cache.CachedPtr = m_CurrentChunk->Buffer + archetype->Offsets[typeIndexInArchetype] - cache.CachedBeginIndex * cache.CachedSizeOf;
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
            cache.CachedEndIndex = cache.CachedBeginIndex + m_CurrentChunk->Count;
            cache.CachedSizeOf = archetype->SizeOfs[typeIndexInArchetype];
            cache.CachedPtr = m_CurrentChunk->Buffer + archetype->Offsets[typeIndexInArchetype] - cache.CachedBeginIndex * cache.CachedSizeOf;
        }
    }
}
