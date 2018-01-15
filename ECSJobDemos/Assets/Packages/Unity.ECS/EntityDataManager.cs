﻿using Unity.Collections;
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
        internal EntityData*   m_Entities;
        int                    m_EntitiesCapacity;
        int                    m_EntitiesFreeIndex;

        public void OnCreate(int capacity)
        {
            m_EntitiesCapacity = capacity;
            m_Entities = (EntityData*)UnsafeUtility.Malloc(m_EntitiesCapacity * sizeof(EntityData), 64, Allocator.Persistent);
            m_EntitiesFreeIndex = 0;
            InitializeAdditionalCapacity(0);
        }

        void InitializeAdditionalCapacity(int start)
        {
            for (int i = start; i != m_EntitiesCapacity - 1; i++)
            {
                m_Entities[i].index = i + 1;
                m_Entities[i].version = 1;
                m_Entities[i].chunk = null;
            }
            m_Entities[m_EntitiesCapacity - 1].index = -1;
        }

        void IncreaseCapacity(bool allowIncreaseCapacity)
        {
            //@TODO: This is not a good long term solution. Better would be to use virtual alloc,
            //       so we can increase the size from any thread...
            if (!allowIncreaseCapacity)
                throw new System.InvalidOperationException("EntityManager.EntityCapacity is not large enough to support the number of created Entities from a Transaction.");

            Capacity = 2 * Capacity;
        }

        public int Capacity
        {
            get { return m_EntitiesCapacity; }
            set
            {
                if (value <= m_EntitiesCapacity)
                    return;

                EntityData* newEntities = (EntityData*) UnsafeUtility.Malloc(value * sizeof(EntityData),
                    64, Allocator.Persistent);
                UnsafeUtility.MemCpy(newEntities, m_Entities, m_EntitiesCapacity * sizeof(EntityData) );
                UnsafeUtility.Free(m_Entities, Allocator.Persistent);

                var startNdx = m_EntitiesCapacity - 1;
                m_Entities = newEntities;
                m_EntitiesCapacity = value;

                InitializeAdditionalCapacity(startNdx);
            }
        }

        public void OnDestroy()
        {
            UnsafeUtility.Free(m_Entities, Allocator.Persistent);
            m_Entities = null;
            m_EntitiesCapacity = 0;
        }

        public bool Exists(Entity entity)
        {
            bool exists = m_Entities[entity.index].version == entity.version;

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            EntityData* entityData = m_Entities + entity.index;
            if (exists && entityData->index >= entityData->chunk->count)
                throw new System.ArgumentException("The entity has been created in a transaction but not yet committed, you are not allowed to access it via EntityManager before calling EntityManager.CommitTransaction();");
            #endif

            return exists;
        }

        public bool ExistsFromTransaction(Entity entity)
        {
            bool exists = m_Entities[entity.index].version == entity.version;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            EntityData* entityData = m_Entities + entity.index;
            if (exists && entityData->index < entityData->chunk->count)
                throw new System.ArgumentException("You are accessing the entity from a transaction, but the entity has already been committed and is thus not available from the transaction. Another thread might otherwise mutate the component data while the transaction job is running.");
#endif
            return exists;
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

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponentFromTransaction(Entity entity, int componentType)
        {
            if (!HasComponentFromTransaction(entity, componentType))
            {
                if (!ExistsFromTransaction(entity))
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
                    Assert.IsTrue(chunk->archetype->sizeOfs[0] == sizeof(Entity) && chunk->archetype->offsets[0] == 0);
                    Entity* movedEntities = (Entity*)(chunk->buffer) + (chunk->count - batchCount);
                    for (int i = 0; i != batchCount;i++)
                        m_Entities[movedEntities[i].index].index = indexInChunk + i;

                    // Move component data from the end to where we deleted components
                    ChunkDataUtility.Copy(chunk, chunk->count - batchCount, chunk, indexInChunk, batchCount);
                }

                if (chunk->managedArrayIndex >= 0)
                {
                    // We can just chop-off the end, no need to copy anything
                    if (chunk->count != indexInChunk + batchCount)
                        ChunkDataUtility.CopyManagedObjects(typeMan, chunk, chunk->count - batchCount, chunk, indexInChunk, batchCount);

                    ChunkDataUtility.ClearManagedObjects(typeMan, chunk, chunk->count - batchCount, batchCount);
                }

                chunk->archetype->entityCount -= batchCount;
                typeMan.SetChunkCount(chunk, chunk->count - batchCount);

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

        public void AllocateEntities(Archetype* arch, Chunk* chunk, int baseIndex, int count, Entity* outputEntities, bool allowIncreaseCapacity)
        {
            Assert.AreEqual(chunk->archetype->offsets[0], 0);
            Assert.AreEqual(chunk->archetype->sizeOfs[0], sizeof(Entity));

            Entity* entityInChunkStart = (Entity*)(chunk->buffer) + baseIndex;

            for (int i = 0; i != count; i++)
            {
                EntityData* entity = m_Entities + m_EntitiesFreeIndex;
                if (entity->index == -1)
                {
                    IncreaseCapacity(allowIncreaseCapacity);
                    entity = m_Entities + m_EntitiesFreeIndex;
                }

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

        public bool HasComponentFromTransaction(Entity entity, int type)
        {
            if (!ExistsFromTransaction (entity))
                return false;

            Archetype* archetype = m_Entities[entity.index].archetype;
            return ChunkDataUtility.GetIndexInTypeArray(archetype, type) != -1;
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            if (!Exists (entity))
                return false;

            Archetype* archetype = m_Entities[entity.index].archetype;

            if (type.IsFixedArray)
            {
                int idx = ChunkDataUtility.GetIndexInTypeArray(archetype, type.typeIndex);
                if (idx == -1)
                    return false;

                return archetype->types[idx].FixedArrayLength == type.FixedArrayLength;
            }
            else
                return ChunkDataUtility.GetIndexInTypeArray(archetype, type.typeIndex) != -1;
        }

        public byte* GetComponentDataWithType(Entity entity, int typeIndex)
        {
            var entityData = m_Entities + entity.index;
            return ChunkDataUtility.GetComponentDataWithType(entityData->chunk, entityData->index, typeIndex);
        }

        public byte* GetComponentDataWithType(Entity entity, int typeIndex, ref int typeLookupCache)
        {
            var entityData = m_Entities + entity.index;
            return ChunkDataUtility.GetComponentDataWithType(entityData->chunk, entityData->index, typeIndex, ref typeLookupCache);
        }

        public void GetComponentDataWithTypeAndFixedArrayLength(Entity entity, int typeIndex, out byte* ptr, out int fixedArrayLength)
        {
            var entityData = m_Entities + entity.index;
            ChunkDataUtility.GetComponentDataWithTypeAndFixedArrayLength(entityData->chunk, entityData->index, typeIndex, out ptr, out fixedArrayLength);
        }

        public Chunk* GetComponentChunk(Entity entity)
        {
            var entityData = m_Entities + entity.index;
            return entityData->chunk;
        }

        public void GetComponentChunk(Entity entity, out Chunk* chunk, out int chunkIndex)
        {
            var entityData = m_Entities + entity.index;
            chunk = entityData->chunk;
            chunkIndex = entityData->index;
        }

        public Archetype* GetArchetype(Entity entity)
        {
            return m_Entities[entity.index].archetype;
        }

        public void SetArchetype(ArchetypeManager typeMan, Entity entity, Archetype* archetype, int* sharedComponentDataIndices)
        {
            Chunk* chunk = typeMan.GetChunkWithEmptySlots(archetype, sharedComponentDataIndices);
            int chunkIndex = typeMan.AllocateIntoChunkImmediate(chunk);

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
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                Entity* lastEntity = (Entity*)ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0);
                m_Entities[lastEntity->index].index = oldChunkIndex;

                ChunkDataUtility.Copy (oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
                if (oldChunk->managedArrayIndex >= 0)
                    ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
            }
            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.ClearManagedObjects(typeMan, oldChunk, lastIndex, 1);

            --oldArchetype->entityCount;
            typeMan.SetChunkCount(oldChunk, lastIndex);
        }

        public void MoveEntityToChunk(ArchetypeManager typeMan, Entity entity, Chunk* newChunk, int newChunkIndex)
        {
            Chunk* oldChunk = m_Entities[entity.index].chunk;
            Assert.IsTrue(oldChunk->archetype == newChunk->archetype);

            int oldChunkIndex = m_Entities[entity.index].index;

            ChunkDataUtility.Copy(oldChunk, oldChunkIndex, newChunk, newChunkIndex, 1);

            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, oldChunkIndex, newChunk, newChunkIndex, 1);

            m_Entities[entity.index].chunk = newChunk;
            m_Entities[entity.index].index = newChunkIndex;

            int lastIndex = oldChunk->count - 1;
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                Entity* lastEntity = (Entity*)ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0);
                m_Entities[lastEntity->index].index = oldChunkIndex;

                ChunkDataUtility.Copy (oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
                if (oldChunk->managedArrayIndex >= 0)
                    ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
            }
            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.ClearManagedObjects(typeMan, oldChunk, lastIndex, 1);

            newChunk->archetype->entityCount--;
            typeMan.SetChunkCount(oldChunk, oldChunk->count - 1);
        }

        public int GetSharedComponentDataIndex(Entity entity, int indexInTypeArray)
        {
            Chunk* chunk = m_Entities[entity.index].chunk;
            int* sharedComponentValueArray = ArchetypeManager.GetSharedComponentValueArray(chunk);
            //TODO: bounds check
            int sharedComponentOffset = m_Entities[entity.index].archetype->sharedComponentOffset[indexInTypeArray];
            return sharedComponentValueArray[sharedComponentOffset];
        }
    }
}
