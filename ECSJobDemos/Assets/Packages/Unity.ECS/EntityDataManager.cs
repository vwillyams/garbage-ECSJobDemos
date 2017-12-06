using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{

    unsafe struct EntityData
    {
        public int version;
        public Archetype* archetype;
        public Chunk* chunk;
        public int index;
    }

    unsafe struct EntityDataManager
    {
        [NativeDisableUnsafePtrRestriction]
        internal EntityData* m_Entities;
        int m_EntitiesCapacity;
        int m_EntitiesFreeIndex;

        public void OnCreate()
        {
            m_EntitiesCapacity = 1000000;
            m_Entities = (EntityData*)UnsafeUtility.Malloc(m_EntitiesCapacity * sizeof(EntityData), 64, Allocator.Persistent);
            for (int i = 0; i != m_EntitiesCapacity - 1; i++)
            {
                m_Entities[i].index = i + 1;
                m_Entities[i].version = 1;
                m_Entities[i].chunk = null;
            }

            m_Entities[m_EntitiesCapacity - 1].index = -1;
            m_EntitiesFreeIndex = 0;
        }

        public void OnDestroy()
        {
            UnsafeUtility.Free((IntPtr)m_Entities, Allocator.Persistent);
            m_Entities = (EntityData*)IntPtr.Zero;
            m_EntitiesCapacity = 0;
        }

        public bool Exists(Entity entity)
        {
            return m_Entities[entity.index].version == entity.version;
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntitiesExist(Entity* entities, int count)
        {
            for (int i = 0; i != count;i++)
            {
                if (!Exists(entities[i]))
                    throw new System.ArgumentException("All entities passed to EntityManager.Destroy must exist. One of the entities was already destroyed or never created.");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponent(Entity entity, ComponentType componentType)
        {
            if (!HasComponent(entity, componentType))
            {
                if (!Exists(entity))
                    throw new System.ArgumentException("The Entity does not exist");
                else if (HasComponent(entity, componentType.typeIndex))
                    throw new System.ArgumentException(string.Format("The component typeof({0}) exists on the entity but the exact type {1} does not", componentType.GetManagedType(), componentType));
                else
                    throw new System.ArgumentException(string.Format("{0} component has not been added to the entity.", componentType));
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponent(Entity entity, int componentType)
        {
            if (!HasComponent(entity, componentType))
            {
                if (!Exists(entity))
                    throw new System.ArgumentException("The entity does not exist");
                else
                //@TODO: Throw with specific type...
                    throw new System.ArgumentException("The component has not been added to the entity.");
            }
        }
        
        public void DeallocateEnties(ArchetypeManager typeMan, Entity* entities, int count)
        {
            while (count != 0)
            {
                /// This is optimized for the case where the array of entities are allocated contigously in the chunk
                /// Thus the compacting of other elements can be batched

                // Calculate baseEntityIndex & chunk
                int baseEntityIndex = entities[0].index;

                Chunk* chunk = m_Entities[baseEntityIndex].chunk;
                int indexInChunk = m_Entities[baseEntityIndex].index;
                int batchCount = 0;

                while (batchCount < count)
                {
                    int entityIndex = entities[batchCount].index;
                    EntityData* data = m_Entities + entityIndex;

                    if (data->chunk != chunk || data->index != indexInChunk + batchCount)
                        break;

                    data->chunk = null;
                    data->version++;
                    data->index = m_EntitiesFreeIndex;
                    m_EntitiesFreeIndex = entityIndex;

                    batchCount++;
                }

                // We can just chop-off the end, no need to copy anything
                if (chunk->count != indexInChunk + batchCount)
                {
                    // updates EntitityData->index to point to where the components will be moved to
                    for (int i = 0; i != batchCount;i++)
                    {
                        Entity* lastEntity = (Entity*)ChunkDataUtility.GetComponentData(chunk, chunk->count - batchCount + i, 0);
                        m_Entities[lastEntity->index].index = indexInChunk + i;
                    }

                    // Move component data from the end to where we deleted components
                    ChunkDataUtility.Copy(chunk, chunk->count - batchCount, chunk, indexInChunk, batchCount);
                    if (chunk->managedArrayIndex >= 0)
                        ChunkDataUtility.CopyManagedObjects(typeMan, chunk, chunk->count - batchCount, chunk, indexInChunk, batchCount);
                }
                
                if (chunk->managedArrayIndex >= 0)
                    ChunkDataUtility.ClearManagedObjects(typeMan, chunk, chunk->count - batchCount, batchCount);

                chunk->count -= batchCount;
                chunk->archetype->entityCount -= batchCount;
                
                //@TODO: When Chunk reaches zero we should probably put the chunk back into a pool,
                // as opposed to keeping it all linked up (which slows down iteration)
                
                entities += batchCount;
                count -= batchCount;
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        void AssertInternalConsistency()
        {
            int aliveEntities = 0;
            int entityType = TypeManager.GetTypeIndex<Entity>();

            for (int i = 0; i != m_EntitiesCapacity; i++)
            {
                if (m_Entities[i].chunk != null)
                {
                    aliveEntities++;
                    
                    Assert.AreEqual(entityType, m_Entities[i].archetype->types[0].typeIndex);
                    Entity entity = *(Entity*)ChunkDataUtility.GetComponentData(m_Entities[i].chunk, m_Entities[i].index, 0);
                    Assert.AreEqual(i, entity.index);
                    Assert.AreEqual(m_Entities[i].version, entity.version);

                    Assert.IsTrue(Exists(entity));
                }
            }

            //@TODO: Validate from perspective of chunks...
        }
#endif

        public void AllocateEntities(Archetype* arch, Chunk* chunk, int baseIndex, int count, Entity* outputEntities)
        {
            int offset = chunk->archetype->offsets[0];
            int stride = chunk->archetype->strides[0];
            Entity* entityInChunkStart = (Entity*)(chunk->buffer + offset + (stride * baseIndex));

            for (int i = 0; i != count; i++)
            {
                EntityData* entity = m_Entities + m_EntitiesFreeIndex;

                outputEntities[i].index = m_EntitiesFreeIndex;
                outputEntities[i].version = entity->version;

                Entity* entityInChunk = entityInChunkStart + i;                

                entityInChunk->index = m_EntitiesFreeIndex;
                entityInChunk->version = entity->version;

                m_EntitiesFreeIndex = entity->index;

                entity->index = baseIndex + i;
                entity->archetype = arch;
                entity->chunk = chunk;
            }
        }

        public bool HasComponent(Entity entity, int type)
        {
            if (!Exists (entity))
                return false;

            Archetype* archetype = m_Entities[entity.index].archetype;
            return ChunkDataUtility.GetIndexInTypeArray(archetype, type) != -1;
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            if (!Exists (entity))
                return false;

            Archetype* archetype = m_Entities[entity.index].archetype;

            if (type.sharedComponentIndex != -1)
            {
                int idx = ChunkDataUtility.GetIndexInTypeArray(archetype, type.typeIndex);
                if (idx == -1)
                    return false;

                return archetype->types[idx].sharedComponentIndex == type.sharedComponentIndex;
            }
            else if (type.IsFixedArray)
            {
                int idx = ChunkDataUtility.GetIndexInTypeArray(archetype, type.typeIndex);
                if (idx == -1)
                    return false;

                return archetype->types[idx].FixedArrayLength == type.FixedArrayLength;
            }
            else
                return ChunkDataUtility.GetIndexInTypeArray(archetype, type.typeIndex) != -1;
        }

        public IntPtr GetComponentDataWithType(Entity entity, int typeIndex)
        {
            return ChunkDataUtility.GetComponentDataWithType(m_Entities[entity.index].chunk, m_Entities[entity.index].index, typeIndex);
        }

        public void GetComponentDataWithTypeAndFixedArrayLength(Entity entity, int typeIndex, out IntPtr ptr, out int fixedArrayLength)
        {
            ChunkDataUtility.GetComponentDataWithTypeAndFixedArrayLength(m_Entities[entity.index].chunk, m_Entities[entity.index].index, typeIndex, out ptr, out fixedArrayLength);
        }

        public void GetComponentChunk(Entity entity, out Chunk* chunk, out int chunkIndex)
        {
            chunk = m_Entities[entity.index].chunk;
            chunkIndex = m_Entities[entity.index].index;
        }

        public Archetype* GetArchetype(Entity entity)
        {
            return m_Entities[entity.index].archetype;
        }

        public void SetArchetype(ArchetypeManager typeMan, Entity entity, Archetype* archetype, Chunk* chunk, int chunkIndex)
        {
            Archetype* oldArchetype = m_Entities[entity.index].archetype;
            Chunk* oldChunk = m_Entities[entity.index].chunk;
            int oldChunkIndex = m_Entities[entity.index].index;
            ChunkDataUtility.Convert(oldChunk, oldChunkIndex, chunk, chunkIndex);
            if (chunk->managedArrayIndex >= 0 && oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, oldChunkIndex, chunk, chunkIndex, 1);

            m_Entities[entity.index].archetype = archetype;
            m_Entities[entity.index].chunk = chunk;
            m_Entities[entity.index].index = chunkIndex;

            int lastIndex = oldChunk->count - 1;
            --oldArchetype->entityCount;
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                Entity lastEntity;
                UnsafeUtility.CopyPtrToStructure (ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0), out lastEntity);
                m_Entities[lastEntity.index].index = oldChunkIndex;

                ChunkDataUtility.Copy (oldChunk, lastIndex, oldChunk, oldChunkIndex);
                if (oldChunk->managedArrayIndex >= 0)
                    ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
            }
            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.ClearManagedObjects(typeMan, oldChunk, lastIndex, 1);

            oldChunk->count = lastIndex;
        }
    }
}



