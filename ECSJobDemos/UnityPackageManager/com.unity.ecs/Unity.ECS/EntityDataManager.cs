//#define USE_BURST_DESTROY

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace UnityEngine.ECS
{
    unsafe struct EntityDataManager
    {
        #if USE_BURST_DESTROY
        unsafe delegate Chunk* DeallocateDataEntitiesInChunkDelegate(EntityDataManager* entityDataManager, Entity* entities, int count, out int indexInChunk, out int batchCount);
        static DeallocateDataEntitiesInChunkDelegate ms_DeallocateDataEntitiesInChunkDelegate;
        #endif

        unsafe struct EntityData
        {
            public int         version;
            public Archetype*  archetype;
            public Chunk*      chunk;
            public int         indexInChunk;
        }
        
        EntityData*            m_Entities;
        int                    m_EntitiesCapacity;
        int                    m_EntitiesFreeIndex;

        public void OnCreate(int capacity)
        {
            m_EntitiesCapacity = capacity;
            m_Entities = (EntityData*)UnsafeUtility.Malloc(m_EntitiesCapacity * sizeof(EntityData), 64, Allocator.Persistent);
            m_EntitiesFreeIndex = 0;
            InitializeAdditionalCapacity(0);

            #if USE_BURST_DESTROY
            if (ms_DeallocateDataEntitiesInChunkDelegate == null)
            {
                ms_DeallocateDataEntitiesInChunkDelegate = DeallocateDataEntitiesInChunk;
                ms_DeallocateDataEntitiesInChunkDelegate = Unity.Burst.BurstDelegateCompiler.CompileDelegate(ms_DeallocateDataEntitiesInChunkDelegate);
            }
            #endif
        }

        void InitializeAdditionalCapacity(int start)
        {
            for (int i = start; i != m_EntitiesCapacity; i++)
            {
                m_Entities[i].indexInChunk = i + 1;
                m_Entities[i].version = 1;
                m_Entities[i].chunk = null;
            }

            // Last entity indexInChunk identifies that we ran out of space...
            m_Entities[m_EntitiesCapacity - 1].indexInChunk = -1;
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
            if (exists && entityData->indexInChunk >= entityData->chunk->count)
                throw new System.ArgumentException("The entity has been created in a transaction but not yet committed, you are not allowed to access it via EntityManager before calling EntityManager.CommitTransaction();");
            #endif

            return exists;
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntitiesExist(Entity* entities, int count)
        {
            for (int i = 0; i != count;i++)
            {
                Entity* entity = entities + i;
                bool exists = m_Entities[entity->index].version == entity->version;
                if (!exists)
                    throw new System.ArgumentException("All entities passed to EntityManager.Destroy must exist. One of the entities was already destroyed or never created.");

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                EntityData* entityData = m_Entities + entity->index;
                if (entityData->indexInChunk >= entityData->chunk->count)
                    throw new System.ArgumentException("The entity has been created in a transaction but not yet committed, you are not allowed to access it via EntityManager before calling EntityManager.CommitTransaction();");
#endif
            }
        }


        public bool ExistsFromTransaction(Entity entity)
        {
            bool exists = m_Entities[entity.index].version == entity.version;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            EntityData* entityData = m_Entities + entity.index;
            if (exists && entityData->indexInChunk < entityData->chunk->count)
                throw new System.ArgumentException("You are accessing the entity from a transaction, but the entity has already been committed and is thus not available from the transaction. Another thread might otherwise mutate the component data while the transaction job is running.");
#endif
            return exists;
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
                int indexInChunk;
                int batchCount;
                Chunk* chunk;

                //Profiler.BeginSample("DeallocateDataEntitiesInChunk");
                fixed (EntityDataManager* manager = &this)
                {
#if USE_BURST_DESTROY
                    chunk = ms_DeallocateDataEntitiesInChunkDelegate(manager , entities, count, out indexInChunk, out batchCount);
#else
                    chunk = DeallocateDataEntitiesInChunk(manager, entities, count, out indexInChunk, out batchCount);
#endif
                }
                //Profiler.EndSample();

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

        static unsafe Chunk* DeallocateDataEntitiesInChunk(EntityDataManager* entityDataManager, Entity* entities, int count, out int indexInChunk, out int batchCount)
        {
            /// This is optimized for the case where the array of entities are allocated contigously in the chunk
            /// Thus the compacting of other elements can be batched

            // Calculate baseEntityIndex & chunk
            int baseEntityIndex = entities[0].index;

            Chunk* chunk = entityDataManager->m_Entities[baseEntityIndex].chunk;
            indexInChunk = entityDataManager->m_Entities[baseEntityIndex].indexInChunk;
            batchCount = 0;

            int freeIndex = entityDataManager->m_EntitiesFreeIndex;
            EntityData* entityDatas = entityDataManager->m_Entities;

            while (batchCount < count)
            {
                int entityIndex = entities[batchCount].index;
                EntityData* data = entityDatas + entityIndex;

                if (data->chunk != chunk || data->indexInChunk != indexInChunk + batchCount)
                    break;

                data->chunk = null;
                data->version++;
                data->indexInChunk = freeIndex;
                freeIndex = entityIndex;

                batchCount++;
            }

            entityDataManager->m_EntitiesFreeIndex = freeIndex;

            // We can just chop-off the end, no need to copy anything
            if (chunk->count != indexInChunk + batchCount)
            {
                // updates EntitityData->indexInChunk to point to where the components will be moved to
                //Assert.IsTrue(chunk->archetype->sizeOfs[0] == sizeof(Entity) && chunk->archetype->offsets[0] == 0);
                Entity* movedEntities = (Entity*) (chunk->buffer) + (chunk->count - batchCount);
                for (int i = 0; i != batchCount; i++)
                    entityDataManager->m_Entities[movedEntities[i].index].indexInChunk = indexInChunk + i;

                // Move component data from the end to where we deleted components
                ChunkDataUtility.Copy(chunk, chunk->count - batchCount, chunk, indexInChunk, batchCount);
            }
            return chunk;
        }

        public static unsafe void FreeDataEntitiesInChunk(EntityDataManager* entityDataManager, Chunk* chunk, int count)
        {
            int freeIndex = entityDataManager->m_EntitiesFreeIndex;
            EntityData* entityDatas = entityDataManager->m_Entities;

            Entity* chunkEntities = (Entity*) chunk->buffer;

            for (int i = 0;i != count;i++)
            {
                int entityIndex = chunkEntities[i].index;
                EntityData* data = entityDatas + entityIndex;

                data->chunk = null;
                data->version++;
                data->indexInChunk = freeIndex;
                freeIndex = entityIndex;
            }

            entityDataManager->m_EntitiesFreeIndex = freeIndex;
        }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public int CheckInternalConsistency()
        {
            int aliveEntities = 0;
            int entityType = TypeManager.GetTypeIndex<Entity>();

            for (int i = 0; i != m_EntitiesCapacity; i++)
            {
                if (m_Entities[i].chunk != null)
                {
                    aliveEntities++;
                    var archetype = m_Entities[i].archetype;
                    Assert.AreEqual(entityType, archetype->types[0].typeIndex);
                    Entity entity = *(Entity*)ChunkDataUtility.GetComponentData(m_Entities[i].chunk, m_Entities[i].indexInChunk, 0);
                    Assert.AreEqual(i, entity.index);
                    Assert.AreEqual(m_Entities[i].version, entity.version);

                    Assert.IsTrue(Exists(entity));
                }
            }

            return aliveEntities;
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
                if (entity->indexInChunk == -1)
                {
                    IncreaseCapacity(allowIncreaseCapacity);
                    entity = m_Entities + m_EntitiesFreeIndex;
                }

                outputEntities[i].index = m_EntitiesFreeIndex;
                outputEntities[i].version = entity->version;

                Entity* entityInChunk = entityInChunkStart + i;

                entityInChunk->index = m_EntitiesFreeIndex;
                entityInChunk->version = entity->version;

                m_EntitiesFreeIndex = entity->indexInChunk;

                entity->indexInChunk = baseIndex + i;
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
            return ChunkDataUtility.GetComponentDataWithType(entityData->chunk, entityData->indexInChunk, typeIndex);
        }

        public byte* GetComponentDataWithType(Entity entity, int typeIndex, ref int typeLookupCache)
        {
            var entityData = m_Entities + entity.index;
            return ChunkDataUtility.GetComponentDataWithType(entityData->chunk, entityData->indexInChunk, typeIndex, ref typeLookupCache);
        }

        public void GetComponentDataWithTypeAndFixedArrayLength(Entity entity, int typeIndex, out byte* ptr, out int fixedArrayLength)
        {
            var entityData = m_Entities + entity.index;
            ChunkDataUtility.GetComponentDataWithTypeAndFixedArrayLength(entityData->chunk, entityData->indexInChunk, typeIndex, out ptr, out fixedArrayLength);
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
            chunkIndex = entityData->indexInChunk;
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
            int oldChunkIndex = m_Entities[entity.index].indexInChunk;
            ChunkDataUtility.Convert(oldChunk, oldChunkIndex, chunk, chunkIndex);
            if (chunk->managedArrayIndex >= 0 && oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, oldChunkIndex, chunk, chunkIndex, 1);

            m_Entities[entity.index].archetype = archetype;
            m_Entities[entity.index].chunk = chunk;
            m_Entities[entity.index].indexInChunk = chunkIndex;

            int lastIndex = oldChunk->count - 1;
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                Entity* lastEntity = (Entity*)ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0);
                m_Entities[lastEntity->index].indexInChunk = oldChunkIndex;

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

            int oldChunkIndex = m_Entities[entity.index].indexInChunk;

            ChunkDataUtility.Copy(oldChunk, oldChunkIndex, newChunk, newChunkIndex, 1);

            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, oldChunkIndex, newChunk, newChunkIndex, 1);

            m_Entities[entity.index].chunk = newChunk;
            m_Entities[entity.index].indexInChunk = newChunkIndex;

            int lastIndex = oldChunk->count - 1;
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                Entity* lastEntity = (Entity*)ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0);
                m_Entities[lastEntity->index].indexInChunk = oldChunkIndex;

                ChunkDataUtility.Copy (oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
                if (oldChunk->managedArrayIndex >= 0)
                    ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
            }
            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.ClearManagedObjects(typeMan, oldChunk, lastIndex, 1);

            newChunk->archetype->entityCount--;
            typeMan.SetChunkCount(oldChunk, oldChunk->count - 1);
        }

        public void CreateEntities(ArchetypeManager archetypeManager, Archetype* archetype, Entity* entities, int count, bool allowIncreaseCapacity)
        {
            while (count != 0)
            {
                Chunk* chunk = archetypeManager.GetChunkWithEmptySlots(archetype, null);
                int allocatedIndex;
                int allocatedCount = archetypeManager.AllocateIntoChunk(chunk, count, out allocatedIndex);
                AllocateEntities(archetype, chunk, allocatedIndex, allocatedCount, entities, allowIncreaseCapacity);
                ChunkDataUtility.ClearComponents(chunk, allocatedIndex, allocatedCount);

                entities += allocatedCount;
                count -= allocatedCount;
            }
        }

        public void InstantiateEntities(ArchetypeManager archetypeManager, Entity srcEntity, Entity* outputEntities, int count, bool allowIncreaseCapacity)
        {
            int srcIndex = m_Entities[srcEntity.index].indexInChunk;
            Chunk* srcChunk = m_Entities[srcEntity.index].chunk;
            Archetype* srcArchetype = m_Entities[srcEntity.index].archetype;
            var srcSharedComponentDataIndices = GetComponentChunk(srcEntity)->GetSharedComponentValueArray();

            while (count != 0)
            {
                Chunk* chunk = archetypeManager.GetChunkWithEmptySlots(srcArchetype, srcSharedComponentDataIndices);
                int indexInChunk;
                int allocatedCount = archetypeManager.AllocateIntoChunk(chunk, count, out indexInChunk);

                ChunkDataUtility.ReplicateComponents(srcChunk, srcIndex, chunk, indexInChunk, allocatedCount);

                AllocateEntities(srcArchetype, chunk, indexInChunk, allocatedCount, outputEntities, allowIncreaseCapacity);

                outputEntities += allocatedCount;
                count -= allocatedCount;
            }
        }

        public int GetSharedComponentDataIndex(Entity entity, int indexInTypeArray)
        {
            Chunk* chunk = m_Entities[entity.index].chunk;
            int* sharedComponentValueArray = chunk->GetSharedComponentValueArray();
            int sharedComponentOffset = m_Entities[entity.index].archetype->sharedComponentOffset[indexInTypeArray];
            return sharedComponentValueArray[sharedComponentOffset];
        }
    }
}
