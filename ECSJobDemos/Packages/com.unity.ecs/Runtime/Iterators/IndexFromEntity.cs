using System;
using System.Security.Policy;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.ECS
{
    public struct EntityIndex
    {
        public int Value;
    }
    
    [NativeContainer]
    public unsafe struct IndexFromEntity
    {
        private readonly EntityGroupData* m_EntityGroupData;
        private readonly EntityManager m_EntityManager;
        private readonly int* m_FilteredSharedComponents;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal IndexFromEntity(EntityManager entityManager, EntityGroupData* entityGroupData, int* filteredSharedComponents, AtomicSafetyHandle safety)
#else
        internal unsafe IndexFromEntity(EntityManager entityManager, EntityGroupData* entityGroupData, int* filteredSharedComponents)
#endif
        {
            m_EntityGroupData = entityGroupData;
            m_EntityManager = entityManager;
            m_FilteredSharedComponents = filteredSharedComponents;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = safety;
#endif
        }

        public EntityIndex this[Entity entity]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                Chunk* entityChunk;
                int entityChunkIndex;
                
                m_EntityManager.m_Entities->GetComponentChunk(entity, out entityChunk, out entityChunkIndex);
                var entityArchetype = m_EntityManager.m_Entities->GetArchetype(entity);

                int entityStartIndex = 0;
                var matchingArchetype = m_EntityGroupData->FirstMatchingArchetype;
                while (true)
                {
                    var archetype = matchingArchetype->Archetype;
                    if ((m_FilteredSharedComponents == null) && (archetype != entityArchetype))
                    {
                        entityStartIndex += archetype->EntityCount;
                    }
                    else
                    {
                        for (var c = (Chunk*)archetype->ChunkList.Begin; c != archetype->ChunkList.End; c = (Chunk*)c->ChunkListNode.Next)
                        {
                            if (c->Count <= 0)
                                continue;
                            
                            if ((m_FilteredSharedComponents != null) && (!ComponentChunkIterator.ChunkMatchesFilter(matchingArchetype, c, m_FilteredSharedComponents)))
                                continue;

                            if (c == entityChunk)
                            {
                                return new EntityIndex {Value = entityStartIndex + entityChunkIndex};
                            }

                            entityStartIndex += c->Count;
                        } 
                    }

                    if (matchingArchetype == m_EntityGroupData->LastMatchingArchetype)
                        break;
                    
                    matchingArchetype = matchingArchetype->Next;
                    if (matchingArchetype == null)
                        break;
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                FailOutOfRangeError(entity);
#endif
                return new EntityIndex {Value = -1};
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        void FailOutOfRangeError(Entity entity)
        {
            throw new IndexOutOfRangeException($"Entity {entity.Index} is out of range of ComponentGroup.");
        }
#endif
    }
}
