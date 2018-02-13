﻿//#define USE_BURST_DESTROY

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.ECS;

namespace Unity.ECS
{
    internal unsafe struct EntityDataManager
    {
#if USE_BURST_DESTROY
        private delegate Chunk* DeallocateDataEntitiesInChunkDelegate(EntityDataManager* entityDataManager, Entity* entities, int count, out int indexInChunk, out int batchCount);
        static DeallocateDataEntitiesInChunkDelegate ms_DeallocateDataEntitiesInChunkDelegate;
#endif

        private struct EntityData
        {
            public int         Version;
            public Archetype*  Archetype;
            public Chunk*      Chunk;
            public int         IndexInChunk;
        }

        private EntityData*   m_Entities;
        private int           m_EntitiesCapacity;
        private int           m_EntitiesFreeIndex;

        private int*          m_ComponentTypeOrderVersion;

        public void OnCreate(int capacity)
        {
            m_EntitiesCapacity = capacity;
            m_Entities =
                (EntityData*) UnsafeUtility.Malloc(m_EntitiesCapacity * sizeof(EntityData), 64, Allocator.Persistent);
            m_EntitiesFreeIndex = 0;
            InitializeAdditionalCapacity(0);

#if USE_BURST_DESTROY
            if (ms_DeallocateDataEntitiesInChunkDelegate == null)
            {
                ms_DeallocateDataEntitiesInChunkDelegate = DeallocateDataEntitiesInChunk;
                ms_DeallocateDataEntitiesInChunkDelegate = Burst.BurstDelegateCompiler.CompileDelegate(ms_DeallocateDataEntitiesInChunkDelegate);
            }
#endif

            const int componentTypeOrderVersionSize = sizeof(int) * TypeManager.MaximumTypesCount;
            m_ComponentTypeOrderVersion = (int*) UnsafeUtility.Malloc(componentTypeOrderVersionSize , UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
            UnsafeUtility.MemClear(m_ComponentTypeOrderVersion, componentTypeOrderVersionSize );
        }

        public void OnDestroy()
        {
            UnsafeUtility.Free(m_Entities, Allocator.Persistent);
            m_Entities = null;
            m_EntitiesCapacity = 0;

            UnsafeUtility.Free(m_ComponentTypeOrderVersion, Allocator.Persistent);
            m_ComponentTypeOrderVersion = null;
        }

        private void InitializeAdditionalCapacity(int start)
        {
            for (var i = start; i != m_EntitiesCapacity; i++)
            {
                m_Entities[i].IndexInChunk = i + 1;
                m_Entities[i].Version = 1;
                m_Entities[i].Chunk = null;
            }

            // Last entity indexInChunk identifies that we ran out of space...
            m_Entities[m_EntitiesCapacity - 1].IndexInChunk = -1;
        }

        private void IncreaseCapacity()
        {
            Capacity = 2 * Capacity;
        }

        public int Capacity
        {
            get { return m_EntitiesCapacity; }
            set
            {
                if (value <= m_EntitiesCapacity)
                    return;

                var newEntities = (EntityData*) UnsafeUtility.Malloc(value * sizeof(EntityData),
                    64, Allocator.Persistent);
                UnsafeUtility.MemCpy(newEntities, m_Entities, m_EntitiesCapacity * sizeof(EntityData));
                UnsafeUtility.Free(m_Entities, Allocator.Persistent);

                var startNdx = m_EntitiesCapacity - 1;
                m_Entities = newEntities;
                m_EntitiesCapacity = value;

                InitializeAdditionalCapacity(startNdx);
            }
        }

        public bool Exists(Entity entity)
        {
            var exists = m_Entities[entity.Index].Version == entity.Version;
            return exists;
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntitiesExist(Entity* entities, int count)
        {
            for (var i = 0; i != count; i++)
            {
                var entity = entities + i;
                var exists = m_Entities[entity->Index].Version == entity->Version;
                if (!exists)
                    throw new System.ArgumentException("All entities passed to EntityManager must exist. One of the entities has already been destroyed or was never created.");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponent(Entity entity, ComponentType componentType)
        {
            if (HasComponent(entity, componentType))
                return;

            if (!Exists(entity))
                throw new System.ArgumentException("The Entity does not exist");

            if (HasComponent(entity, componentType.TypeIndex))
                throw new System.ArgumentException(
                    $"The component typeof({componentType.GetManagedType()}) exists on the entity but the exact type {componentType} does not");

            throw new System.ArgumentException($"{componentType} component has not been added to the entity.");
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void AssertEntityHasComponent(Entity entity, int componentType)
        {
            if (HasComponent(entity, componentType))
                return;

            if (!Exists(entity))
                throw new System.ArgumentException("The entity does not exist");

            throw new System.ArgumentException("The component has not been added to the entity.");
        }

        public void DeallocateEnties(ArchetypeManager typeMan, SharedComponentDataManager sharedComponentDataManager, Entity* entities, int count)
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

                IncrementComponentOrderVersion(chunk->archetype, chunk, sharedComponentDataManager);

                if (chunk->managedArrayIndex >= 0)
                {
                    // We can just chop-off the end, no need to copy anything
                    if (chunk->count != indexInChunk + batchCount)
                        ChunkDataUtility.CopyManagedObjects(typeMan, chunk, chunk->count - batchCount, chunk,
                            indexInChunk, batchCount);

                    ChunkDataUtility.ClearManagedObjects(typeMan, chunk, chunk->count - batchCount, batchCount);
                }

                chunk->archetype->entityCount -= batchCount;
                typeMan.SetChunkCount(chunk, chunk->count - batchCount);

                entities += batchCount;
                count -= batchCount;
            }
        }

        private static Chunk* DeallocateDataEntitiesInChunk(EntityDataManager* entityDataManager, Entity* entities,
            int count, out int indexInChunk, out int batchCount)
        {
            /// This is optimized for the case where the array of entities are allocated contigously in the chunk
            /// Thus the compacting of other elements can be batched

            // Calculate baseEntityIndex & chunk
            var baseEntityIndex = entities[0].Index;

            var chunk = entityDataManager->m_Entities[baseEntityIndex].Chunk;
            indexInChunk = entityDataManager->m_Entities[baseEntityIndex].IndexInChunk;
            batchCount = 0;

            var freeIndex = entityDataManager->m_EntitiesFreeIndex;
            var entityDatas = entityDataManager->m_Entities;

            while (batchCount < count)
            {
                var entityIndex = entities[batchCount].Index;
                var data = entityDatas + entityIndex;

                if (data->Chunk != chunk || data->IndexInChunk != indexInChunk + batchCount)
                    break;

                data->Chunk = null;
                data->Version++;
                data->IndexInChunk = freeIndex;
                freeIndex = entityIndex;

                batchCount++;
            }

            entityDataManager->m_EntitiesFreeIndex = freeIndex;

            // We can just chop-off the end, no need to copy anything
            if (chunk->count == indexInChunk + batchCount)
                return chunk;

            // updates EntitityData->indexInChunk to point to where the components will be moved to
            //Assert.IsTrue(chunk->archetype->sizeOfs[0] == sizeof(Entity) && chunk->archetype->offsets[0] == 0);
            var movedEntities = (Entity*) (chunk->buffer) + (chunk->count - batchCount);
            for (var i = 0; i != batchCount; i++)
                entityDataManager->m_Entities[movedEntities[i].Index].IndexInChunk = indexInChunk + i;

            // Move component data from the end to where we deleted components
            ChunkDataUtility.Copy(chunk, chunk->count - batchCount, chunk, indexInChunk, batchCount);

            return chunk;
        }

        public static void FreeDataEntitiesInChunk(EntityDataManager* entityDataManager, Chunk* chunk, int count)
        {
            var freeIndex = entityDataManager->m_EntitiesFreeIndex;
            var entityDatas = entityDataManager->m_Entities;

            var chunkEntities = (Entity*) chunk->buffer;

            for (var i = 0; i != count; i++)
            {
                var entityIndex = chunkEntities[i].Index;
                var data = entityDatas + entityIndex;

                data->Chunk = null;
                data->Version++;
                data->IndexInChunk = freeIndex;
                freeIndex = entityIndex;
            }

            entityDataManager->m_EntitiesFreeIndex = freeIndex;
        }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public int CheckInternalConsistency()
        {
            var aliveEntities = 0;
            var entityType = TypeManager.GetTypeIndex<Entity>();

            for (var i = 0; i != m_EntitiesCapacity; i++)
            {
                if (m_Entities[i].Chunk == null)
                    continue;

                aliveEntities++;
                var archetype = m_Entities[i].Archetype;
                Assert.AreEqual(entityType, archetype->types[0].typeIndex);
                var entity =
                    *(Entity*) ChunkDataUtility.GetComponentData(m_Entities[i].Chunk, m_Entities[i].IndexInChunk,
                        0);
                Assert.AreEqual(i, entity.Index);
                Assert.AreEqual(m_Entities[i].Version, entity.Version);

                Assert.IsTrue(Exists(entity));
            }

            return aliveEntities;
        }
#endif

        public void AllocateEntities(Archetype* arch, Chunk* chunk, int baseIndex, int count, Entity* outputEntities)
        {
            Assert.AreEqual(chunk->archetype->offsets[0], 0);
            Assert.AreEqual(chunk->archetype->sizeOfs[0], sizeof(Entity));

            var entityInChunkStart = (Entity*) (chunk->buffer) + baseIndex;

            for (var i = 0; i != count; i++)
            {
                var entity = m_Entities + m_EntitiesFreeIndex;
                if (entity->IndexInChunk == -1)
                {
                    IncreaseCapacity();
                    entity = m_Entities + m_EntitiesFreeIndex;
                }

                outputEntities[i].Index = m_EntitiesFreeIndex;
                outputEntities[i].Version = entity->Version;

                var entityInChunk = entityInChunkStart + i;

                entityInChunk->Index = m_EntitiesFreeIndex;
                entityInChunk->Version = entity->Version;

                m_EntitiesFreeIndex = entity->IndexInChunk;

                entity->IndexInChunk = baseIndex + i;
                entity->Archetype = arch;
                entity->Chunk = chunk;
            }
        }

        public bool HasComponent(Entity entity, int type)
        {
            if (!Exists(entity))
                return false;

            var archetype = m_Entities[entity.Index].Archetype;
            return ChunkDataUtility.GetIndexInTypeArray(archetype, type) != -1;
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            if (!Exists(entity))
                return false;

            var archetype = m_Entities[entity.Index].Archetype;

            if (!type.IsFixedArray)
                return ChunkDataUtility.GetIndexInTypeArray(archetype, type.TypeIndex) != -1;

            var idx = ChunkDataUtility.GetIndexInTypeArray(archetype, type.TypeIndex);
            if (idx == -1)
                return false;

            return archetype->types[idx].FixedArrayLength == type.FixedArrayLength;

        }

        public byte* GetComponentDataWithType(Entity entity, int typeIndex)
        {
            var entityData = m_Entities + entity.Index;
            return ChunkDataUtility.GetComponentDataWithType(entityData->Chunk, entityData->IndexInChunk, typeIndex);
        }

        public byte* GetComponentDataWithType(Entity entity, int typeIndex, ref int typeLookupCache)
        {
            var entityData = m_Entities + entity.Index;
            return ChunkDataUtility.GetComponentDataWithType(entityData->Chunk, entityData->IndexInChunk, typeIndex,
                ref typeLookupCache);
        }

        public void GetComponentDataWithTypeAndFixedArrayLength(Entity entity, int typeIndex, out byte* ptr,
            out int fixedArrayLength)
        {
            var entityData = m_Entities + entity.Index;
            ChunkDataUtility.GetComponentDataWithTypeAndFixedArrayLength(entityData->Chunk, entityData->IndexInChunk,
                typeIndex, out ptr, out fixedArrayLength);
        }

        public Chunk* GetComponentChunk(Entity entity)
        {
            var entityData = m_Entities + entity.Index;
            return entityData->Chunk;
        }

        public void GetComponentChunk(Entity entity, out Chunk* chunk, out int chunkIndex)
        {
            var entityData = m_Entities + entity.Index;
            chunk = entityData->Chunk;
            chunkIndex = entityData->IndexInChunk;
        }

        public Archetype* GetArchetype(Entity entity)
        {
            return m_Entities[entity.Index].Archetype;
        }

        public void SetArchetype(ArchetypeManager typeMan, Entity entity, Archetype* archetype,
            int* sharedComponentDataIndices)
        {
            var chunk = typeMan.GetChunkWithEmptySlots(archetype, sharedComponentDataIndices);
            var chunkIndex = typeMan.AllocateIntoChunk(chunk);

            var oldArchetype = m_Entities[entity.Index].Archetype;
            var oldChunk = m_Entities[entity.Index].Chunk;
            var oldChunkIndex = m_Entities[entity.Index].IndexInChunk;
            ChunkDataUtility.Convert(oldChunk, oldChunkIndex, chunk, chunkIndex);
            if (chunk->managedArrayIndex >= 0 && oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, oldChunkIndex, chunk, chunkIndex, 1);

            m_Entities[entity.Index].Archetype = archetype;
            m_Entities[entity.Index].Chunk = chunk;
            m_Entities[entity.Index].IndexInChunk = chunkIndex;

            var lastIndex = oldChunk->count - 1;
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                var lastEntity = (Entity*) ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0);
                m_Entities[lastEntity->Index].IndexInChunk = oldChunkIndex;

                ChunkDataUtility.Copy(oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
                if (oldChunk->managedArrayIndex >= 0)
                    ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
            }

            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.ClearManagedObjects(typeMan, oldChunk, lastIndex, 1);

            --oldArchetype->entityCount;
            typeMan.SetChunkCount(oldChunk, lastIndex);
        }

        public void AddComponent(Entity entity, ComponentType type, ArchetypeManager archetypeManager, SharedComponentDataManager sharedComponentDataManager,
            EntityGroupManager groupManager, ComponentTypeInArchetype* componentTypeInArchetypeArray)
        {
            var componentType = new ComponentTypeInArchetype(type);
            var archetype = GetArchetype(entity);

            var t = 0;
            while (t < archetype->typesCount && archetype->types[t] < componentType)
            {
                componentTypeInArchetypeArray[t] = archetype->types[t];
                ++t;
            }

            componentTypeInArchetypeArray[t] = componentType;
            while (t < archetype->typesCount)
            {
                componentTypeInArchetypeArray[t + 1] = archetype->types[t];
                ++t;
            }

            var newType = archetypeManager.GetOrCreateArchetype(componentTypeInArchetypeArray,
                archetype->typesCount + 1, groupManager);

            int* sharedComponentDataIndices = null;
            if (newType->numSharedComponents > 0)
            {
                var oldSharedComponentDataIndices = GetComponentChunk(entity)->sharedComponentValueArray;
                var newComponentIsShared = (TypeManager.TypeCategory.ISharedComponentData ==
                                            TypeManager.GetComponentType(type.TypeIndex).Category);
                if (newComponentIsShared)
                {
                    int* stackAlloced = stackalloc int[newType->numSharedComponents];
                    sharedComponentDataIndices = stackAlloced;

                    if (archetype->sharedComponentOffset == null)
                    {
                        sharedComponentDataIndices[0] = 0;
                    }
                    else
                    {
                        t = 0;
                        var sharedIndex = 0;
                        while (t < archetype->typesCount && archetype->types[t] < componentType)
                        {
                            if (archetype->sharedComponentOffset[t] != -1)
                            {
                                sharedComponentDataIndices[sharedIndex] = oldSharedComponentDataIndices[sharedIndex];
                                ++sharedIndex;
                            }

                            ++t;
                        }

                        sharedComponentDataIndices[sharedIndex] = 0;
                        while (t < archetype->typesCount)
                        {
                            if (archetype->sharedComponentOffset[t] != -1)
                            {
                                sharedComponentDataIndices[sharedIndex + 1] =
                                    oldSharedComponentDataIndices[sharedIndex];
                                ++sharedIndex;
                            }

                            ++t;
                        }
                    }
                }
                else
                {
                    // reuse old sharedComponentDataIndices
                    sharedComponentDataIndices = oldSharedComponentDataIndices;
                }
            }

            SetArchetype(archetypeManager, entity, newType, sharedComponentDataIndices);

            IncrementComponentOrderVersion(newType, GetComponentChunk(entity), sharedComponentDataManager);
        }

        public void RemoveComponent(Entity entity, ComponentType type, ArchetypeManager archetypeManager, SharedComponentDataManager sharedComponentDataManager,
            EntityGroupManager groupManager, ComponentTypeInArchetype* componentTypeInArchetypeArray)
        {
            var componentType = new ComponentTypeInArchetype(type);

            var archetype = GetArchetype(entity);

            var removedTypes = 0;
            for (var t = 0; t < archetype->typesCount; ++t)
            {
                if (archetype->types[t].typeIndex == componentType.typeIndex)
                    ++removedTypes;
                else
                    componentTypeInArchetypeArray[t - removedTypes] = archetype->types[t];
            }

            Assert.AreNotEqual(-1, removedTypes);

            var newType = archetypeManager.GetOrCreateArchetype(componentTypeInArchetypeArray,
                archetype->typesCount - removedTypes, groupManager);

            int* sharedComponentDataIndices = null;
            if (newType->numSharedComponents > 0)
            {
                var oldSharedComponentDataIndices = GetComponentChunk(entity)->sharedComponentValueArray;
                var removedComponentIsShared = TypeManager.TypeCategory.ISharedComponentData ==
                                                TypeManager.GetComponentType(type.TypeIndex).Category;
                removedTypes = 0;
                if (removedComponentIsShared)
                {
                    int* tempAlloc = stackalloc int[newType->numSharedComponents];
                    sharedComponentDataIndices = tempAlloc;
                    for (var t = 0; t < archetype->typesCount; ++t)
                    {
                        if (archetype->types[t].typeIndex == componentType.typeIndex)
                            ++removedTypes;
                        else
                            sharedComponentDataIndices[t - removedTypes] = oldSharedComponentDataIndices[t];
                    }
                }
                else
                {
                    // reuse old sharedComponentDataIndices
                    sharedComponentDataIndices = oldSharedComponentDataIndices;
                }
            }

            IncrementComponentOrderVersion(archetype, GetComponentChunk(entity), sharedComponentDataManager);

            SetArchetype(archetypeManager, entity, newType, sharedComponentDataIndices);
        }

        public void MoveEntityToChunk(ArchetypeManager typeMan, Entity entity, Chunk* newChunk, int newChunkIndex)
        {
            var oldChunk = m_Entities[entity.Index].Chunk;
            Assert.IsTrue(oldChunk->archetype == newChunk->archetype);

            var oldChunkIndex = m_Entities[entity.Index].IndexInChunk;

            ChunkDataUtility.Copy(oldChunk, oldChunkIndex, newChunk, newChunkIndex, 1);

            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, oldChunkIndex, newChunk, newChunkIndex, 1);

            m_Entities[entity.Index].Chunk = newChunk;
            m_Entities[entity.Index].IndexInChunk = newChunkIndex;

            var lastIndex = oldChunk->count - 1;
            // No need to replace with ourselves
            if (lastIndex != oldChunkIndex)
            {
                var lastEntity = (Entity*) ChunkDataUtility.GetComponentData(oldChunk, lastIndex, 0);
                m_Entities[lastEntity->Index].IndexInChunk = oldChunkIndex;

                ChunkDataUtility.Copy(oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
                if (oldChunk->managedArrayIndex >= 0)
                    ChunkDataUtility.CopyManagedObjects(typeMan, oldChunk, lastIndex, oldChunk, oldChunkIndex, 1);
            }

            if (oldChunk->managedArrayIndex >= 0)
                ChunkDataUtility.ClearManagedObjects(typeMan, oldChunk, lastIndex, 1);

            newChunk->archetype->entityCount--;
            typeMan.SetChunkCount(oldChunk, oldChunk->count - 1);
        }

        public void CreateEntities(ArchetypeManager archetypeManager, Archetype* archetype, Entity* entities, int count)
        {
            while (count != 0)
            {
                var chunk = archetypeManager.GetChunkWithEmptySlots(archetype, null);
                int allocatedIndex;
                var allocatedCount = archetypeManager.AllocateIntoChunk(chunk, count, out allocatedIndex);
                AllocateEntities(archetype, chunk, allocatedIndex, allocatedCount, entities);
                ChunkDataUtility.ClearComponents(chunk, allocatedIndex, allocatedCount);

                entities += allocatedCount;
                count -= allocatedCount;
            }

            IncrementComponentTypeOrderVersion(archetype);
        }

        public void InstantiateEntities(ArchetypeManager archetypeManager, SharedComponentDataManager sharedComponentDataManager, Entity srcEntity, Entity* outputEntities, int count)
        {
            var srcIndex = m_Entities[srcEntity.Index].IndexInChunk;
            var srcChunk = m_Entities[srcEntity.Index].Chunk;
            var srcArchetype = m_Entities[srcEntity.Index].Archetype;
            var srcSharedComponentDataIndices = GetComponentChunk(srcEntity)->sharedComponentValueArray;

            while (count != 0)
            {
                var chunk = archetypeManager.GetChunkWithEmptySlots(srcArchetype, srcSharedComponentDataIndices);
                int indexInChunk;
                var allocatedCount = archetypeManager.AllocateIntoChunk(chunk, count, out indexInChunk);

                ChunkDataUtility.ReplicateComponents(srcChunk, srcIndex, chunk, indexInChunk, allocatedCount);

                AllocateEntities(srcArchetype, chunk, indexInChunk, allocatedCount, outputEntities);

                outputEntities += allocatedCount;
                count -= allocatedCount;
            }

            IncrementComponentOrderVersion(srcArchetype, srcChunk, sharedComponentDataManager);
        }

        public int GetSharedComponentDataIndex(Entity entity, int typeIndex)
        {
            var archetype = GetArchetype(entity);
            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            var chunk = m_Entities[entity.Index].Chunk;
            var sharedComponentValueArray = chunk->sharedComponentValueArray;
            var sharedComponentOffset = m_Entities[entity.Index].Archetype->sharedComponentOffset[indexInTypeArray];
            return sharedComponentValueArray[sharedComponentOffset];
        }

        public void SetSharedComponentDataIndex(ArchetypeManager archetypeManager, SharedComponentDataManager sharedComponentDataManager, Entity entity, int typeIndex, int newSharedComponentDataIndex)
        {
            var archetype = GetArchetype(entity);

            var indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            var srcChunk = GetComponentChunk(entity);
            var srcSharedComponentValueArray = srcChunk->sharedComponentValueArray;
            var sharedComponentOffset = archetype->sharedComponentOffset[indexInTypeArray];
            var oldSharedComponentDataIndex = srcSharedComponentValueArray[sharedComponentOffset];

            if (newSharedComponentDataIndex == oldSharedComponentDataIndex)
                return;

            var sharedComponentIndices = stackalloc int[archetype->numSharedComponents];
            var srcSharedComponentDataIndices = srcChunk->sharedComponentValueArray;

            ArchetypeManager.CopySharedComponentDataIndexArray(sharedComponentIndices, srcSharedComponentDataIndices, archetype->numSharedComponents);
            sharedComponentIndices[sharedComponentOffset] = newSharedComponentDataIndex;

            var newChunk = archetypeManager.GetChunkWithEmptySlots(archetype, sharedComponentIndices);
            var newChunkIndex = archetypeManager.AllocateIntoChunk(newChunk);

            IncrementComponentOrderVersion(archetype, srcChunk, sharedComponentDataManager);

            MoveEntityToChunk(archetypeManager, entity, newChunk, newChunkIndex);
        }

        private void IncrementComponentOrderVersion(Archetype* archetype, Chunk* chunk, SharedComponentDataManager sharedComponentDataManager)
        {
            // Increment shared component version
            var sharedComponentDataIndices = chunk->sharedComponentValueArray;
            for (var i = 0; i < archetype->numSharedComponents; i++)
            {
                sharedComponentDataManager.IncrementSharedComponentVersion(sharedComponentDataIndices[i]);
            }

            IncrementComponentTypeOrderVersion(archetype);
        }

        private void IncrementComponentTypeOrderVersion(Archetype* archetype)
        {
            // Increment type component version
            for (var t = 0; t < archetype->typesCount; ++t)
            {
                var typeIndex = archetype->types[t].typeIndex;
                m_ComponentTypeOrderVersion[typeIndex]++;
            }
        }

        public int GetComponentTypeOrderVersion(int typeIndex)
        {
            return m_ComponentTypeOrderVersion[typeIndex];
        }
    }
}
