﻿using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using UnityEngine.Profiling;

namespace UnityEngine.ECS
{
    //@TODO: safety?
    public unsafe struct EntityArchetype
    {
        internal Archetype* archetype;

        public static bool operator ==(EntityArchetype lhs, EntityArchetype rhs) { return lhs.archetype == rhs.archetype; }
        public static bool operator !=(EntityArchetype lhs, EntityArchetype rhs) { return lhs.archetype != rhs.archetype; }
        public override bool Equals(object compare) { return this == (EntityArchetype)compare; }
        public override int GetHashCode() { return (int)archetype; }
    }


    public struct Entity
    {
        public int index;
        public int version;

        public static bool operator ==(Entity lhs, Entity rhs) { return lhs.index == rhs.index && lhs.version == rhs.version; }
        public static bool operator !=(Entity lhs, Entity rhs) { return lhs.index != rhs.index || lhs.version != rhs.version; }
        public override bool Equals(object compare) { return this == (Entity)compare; }
        public override int GetHashCode() { return index; }

        public static Entity Null
        {
            get { return new Entity(); }
        }
    }

    public class EntityManager : ScriptBehaviourManager
    {
        EntityDataManager m_Entities;

        internal ArchetypeManager m_ArchetypeManager;
        EntityGroupManager m_GroupManager;
        ComponentJobSafetyManager m_JobSafetyManager;

        internal SharedComponentDataManager m_SharedComponentManager;

        unsafe ComponentType*             m_CachedComponentTypeArray;
        unsafe ComponentTypeInArchetype*  m_CachedComponentTypeInArchetypeArray;

        protected sealed override void OnCreateManagerInternal(int capacity)
        {
        }

        unsafe protected override void OnCreateManager(int capacity)
        {
            m_Entities.OnCreate(capacity);
            m_ArchetypeManager = new ArchetypeManager();
            m_JobSafetyManager = new ComponentJobSafetyManager();
            m_GroupManager = new EntityGroupManager(m_JobSafetyManager);
            m_SharedComponentManager = new SharedComponentDataManager();
            TypeManager.Initialize();

            m_CachedComponentTypeArray = (ComponentType*)UnsafeUtility.Malloc(sizeof(ComponentType) * 32 * 1024, 16, Allocator.Persistent);
            m_CachedComponentTypeInArchetypeArray = (ComponentTypeInArchetype*)UnsafeUtility.Malloc(sizeof(ComponentTypeInArchetype) * 32 * 1024, 16, Allocator.Persistent);
        }

        unsafe protected override void OnDestroyManager()
        {
            m_JobSafetyManager.Dispose(); m_JobSafetyManager = null;
            m_SharedComponentManager.Dispose(); m_SharedComponentManager = null;
            m_Entities.OnDestroy();
            m_ArchetypeManager.Dispose(); m_ArchetypeManager = null;
            m_GroupManager.Dispose(); m_GroupManager = null;

            UnsafeUtility.Free(m_CachedComponentTypeArray, Allocator.Persistent);
            m_CachedComponentTypeArray = null;

            UnsafeUtility.Free(m_CachedComponentTypeInArchetypeArray, Allocator.Persistent);
            m_CachedComponentTypeInArchetypeArray = null;
        }

        internal override void InternalUpdate()
        {
        }

        unsafe public bool IsCreated { get { return (m_CachedComponentTypeArray != null); } }

        unsafe int PopulatedCachedTypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeArray[0] = ComponentType.Create<Entity>();
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        unsafe int PopulatedCachedTypeInArchetypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeInArchetypeArray[0] = new ComponentTypeInArchetype(ComponentType.Create<Entity>());
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeInArchetypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        unsafe public ComponentGroup CreateComponentGroup(params ComponentType[] requiredComponents)
        {
            return m_GroupManager.CreateEntityGroup(this, m_CachedComponentTypeArray, PopulatedCachedTypeArray(requiredComponents), new TransformAccessArray());
        }

        unsafe public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            EntityArchetype type;
            type.archetype = m_ArchetypeManager.GetArchetype(m_CachedComponentTypeInArchetypeArray, PopulatedCachedTypeInArchetypeArray(types), m_GroupManager, m_SharedComponentManager);
            return type;
        }

        unsafe public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            CreateEntityInternal(archetype, (Entity*)entities.GetUnsafePtr(), entities.Length);
        }

        unsafe public Entity CreateEntity(EntityArchetype archetype)
        {
            Entity entity;
            CreateEntityInternal(archetype, &entity, 1);
            return entity;
        }

        unsafe public Entity CreateEntity(params ComponentType[] types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        unsafe void CreateEntityInternal(EntityArchetype archetype, Entity* entities, int count)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            while (count != 0)
            {
                Chunk* chunk = m_ArchetypeManager.GetChunkWithEmptySlots(archetype.archetype, null);
                int allocatedIndex;
                int allocatedCount = m_ArchetypeManager.AllocateIntoChunk(chunk, count, out allocatedIndex);
                m_Entities.AllocateEntities(archetype.archetype, chunk, allocatedIndex, allocatedCount, entities);
                ChunkDataUtility.ClearComponents(chunk, allocatedIndex, allocatedCount);

                entities += allocatedCount;
                count -= allocatedCount;
            }
        }

        unsafe public void DestroyEntity(NativeArray<Entity> entities)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            m_Entities.AssertEntitiesExist((Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);

            m_Entities.DeallocateEnties(m_ArchetypeManager, (Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        unsafe public void DestroyEntity(Entity entity)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();
            m_Entities.AssertEntitiesExist(&entity, 1);

            m_Entities.DeallocateEnties(m_ArchetypeManager, &entity, 1);
        }

        public bool Exists(Entity entity)
        {
            return m_Entities.Exists(entity);
        }

        public bool HasComponent<T>(Entity entity)
        {
            return m_Entities.HasComponent(entity, ComponentType.Create<T>());
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            return m_Entities.HasComponent(entity, type);
        }

        public unsafe Entity Instantiate(Entity srcEntity)
        {
            Entity entity;
            InstantiateInternal(srcEntity, &entity, 1);
            return entity;
        }

        public unsafe Entity Instantiate(GameObject srcGameObject)
        {
            var components = srcGameObject.GetComponents<ComponentDataWrapperBase>();
            var componentTypes = new ComponentType[components.Length];
            for (int t = 0; t != components.Length; ++t)
                componentTypes[t] = components[t].GetComponentType(this);

            var srcEntity = CreateEntity(componentTypes);
            for (int t = 0; t != components.Length; ++t)
                components[t].UpdateComponentData(this, srcEntity);

            return srcEntity;
        }

        public unsafe void Instantiate(GameObject srcGameObject, NativeArray<Entity> outputEntities)
        {
            if (outputEntities.Length == 0)
                return;

            var entity = Instantiate(srcGameObject);
            outputEntities[0] = entity;

            Entity* entityPtr = (Entity*)outputEntities.GetUnsafePtr();
            InstantiateInternal(entity, entityPtr + 1, outputEntities.Length - 1);
        }

        public unsafe void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            InstantiateInternal(srcEntity, (Entity*)outputEntities.GetUnsafePtr(), outputEntities.Length);
        }


        unsafe void InstantiateInternal(Entity srcEntity, Entity* outputEntities, int count)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            if (!m_Entities.Exists(srcEntity))
                throw new System.ArgumentException("srcEntity is not a valid entity");

            int srcIndex = m_Entities.m_Entities[srcEntity.index].index;
            Chunk* srcChunk = m_Entities.m_Entities[srcEntity.index].chunk;
            Archetype* srcArchetype = m_Entities.m_Entities[srcEntity.index].archetype;
            var srcSharedComponentDataIndices = ArchetypeManager.GetSharedComponentValueArray(m_Entities.GetComponentChunk(srcEntity));

            while (count != 0)
            {
                Chunk* chunk = m_ArchetypeManager.GetChunkWithEmptySlots(srcArchetype, srcSharedComponentDataIndices);
                int indexInChunk;
                int allocatedCount = m_ArchetypeManager.AllocateIntoChunk(chunk, count, out indexInChunk);

                ChunkDataUtility.ReplicateComponents(srcChunk, srcIndex, chunk, indexInChunk, allocatedCount);

                m_Entities.AllocateEntities(srcArchetype, chunk, indexInChunk, allocatedCount, outputEntities);

                outputEntities += allocatedCount;
                count -= allocatedCount;
            }
        }

        public unsafe void AddComponent(Entity entity, ComponentType type)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            m_Entities.AssertEntitiesExist(&entity, 1);

            //@TODO: Handle ISharedComponentData
            var componentType = new ComponentTypeInArchetype(type);
            Archetype* archetype = m_Entities.GetArchetype(entity);
            int t = 0;
            while (t < archetype->typesCount && archetype->types[t] < componentType)
            {
                m_CachedComponentTypeInArchetypeArray[t] = archetype->types[t];
                ++t;
            }

            m_CachedComponentTypeInArchetypeArray[t] = componentType;
            while (t < archetype->typesCount)
            {
                m_CachedComponentTypeInArchetypeArray[t + 1] = archetype->types[t];
                ++t;
            }
            Archetype* newType = m_ArchetypeManager.GetArchetype(m_CachedComponentTypeInArchetypeArray, archetype->typesCount + 1, m_GroupManager, m_SharedComponentManager);

            bool newComponentIsShared = false;;

            int* sharedComponentDataIndices = null;

            if (newType->numSharedComponents > 0)
            {
                var oldSharedComponentDataIndices = ArchetypeManager.GetSharedComponentValueArray(m_Entities.GetComponentChunk(entity));
                newComponentIsShared = (TypeManager.TypeCategory.ISharedComponentData == TypeManager.GetComponentType(type.typeIndex).category);
                if (newComponentIsShared)
                {
                    sharedComponentDataIndices = (int*)UnsafeUtility.Malloc(sizeof(int) * newType->numSharedComponents, 4, Allocator.Temp);
                    t = 0;
                    while (t < archetype->typesCount && archetype->types[t] < componentType)
                    {
                        if (archetype->sharedComponentOffset[t] != -1)
                            sharedComponentDataIndices[t] = oldSharedComponentDataIndices[t];
                                ++t;
                    }

                    sharedComponentDataIndices[t] = componentType.typeIndex;
                    while (t < archetype->typesCount)
                    {
                        sharedComponentDataIndices[t + 1] = oldSharedComponentDataIndices[t];
                        ++t;
                    }
                }
                else
                {
                    // reuse old sharedComponentDataIndices
                    sharedComponentDataIndices = oldSharedComponentDataIndices;
                }
            }
            Chunk* newChunk = m_ArchetypeManager.GetChunkWithEmptySlots(newType, sharedComponentDataIndices);

            int newChunkIndex = m_ArchetypeManager.AllocateIntoChunk(newChunk);
            m_Entities.SetArchetype(m_ArchetypeManager, entity, newType, newChunk, newChunkIndex);

            if(newComponentIsShared)
                UnsafeUtility.Free(sharedComponentDataIndices, Allocator.Temp);
        }

        public unsafe void RemoveComponent(Entity entity, ComponentType type)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            var componentType = new ComponentTypeInArchetype(type);

            m_Entities.AssertEntityHasComponent(entity, type);

            Archetype* archtype = m_Entities.GetArchetype(entity);
            int removedTypes = 0;
            for (int t = 0; t < archtype->typesCount; ++t)
            {
                if (archtype->types[t].typeIndex == componentType.typeIndex)
                    ++removedTypes;
                else
                    m_CachedComponentTypeInArchetypeArray[t - removedTypes] = archtype->types[t];
            }

            Assertions.Assert.AreNotEqual(-1, removedTypes);

            bool freeSharedComponent = false;

            Archetype* newType = m_ArchetypeManager.GetArchetype(m_CachedComponentTypeInArchetypeArray, archtype->typesCount - removedTypes, m_GroupManager, m_SharedComponentManager);

            int* sharedComponentDataIndices = null;

            if (newType->numSharedComponents > 0)
            {
                var oldSharedComponentDataIndices = ArchetypeManager.GetSharedComponentValueArray(m_Entities.GetComponentChunk(entity));
                bool removedComponentIsShared = (TypeManager.TypeCategory.ISharedComponentData == TypeManager.GetComponentType(type.typeIndex).category);
                removedTypes = 0;
                if (removedComponentIsShared)
                {
                    freeSharedComponent = true;
                    sharedComponentDataIndices = (int*)UnsafeUtility.Malloc(sizeof(int) * newType->numSharedComponents, 4, Allocator.Temp);
                    for (int t = 0; t < archtype->typesCount; ++t)
                    {
                        if (archtype->types[t].typeIndex == componentType.typeIndex)
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

            Chunk* newChunk = m_ArchetypeManager.GetChunkWithEmptySlots(newType, sharedComponentDataIndices);
            int newChunkIndex = m_ArchetypeManager.AllocateIntoChunk(newChunk);
            m_Entities.SetArchetype(m_ArchetypeManager, entity, newType, newChunk, newChunkIndex);
        }

        public void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            AddComponent(entity, ComponentType.Create<T>());
            SetComponent<T>(entity, componentData);
        }

        public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
        {
            RemoveComponent(entity, ComponentType.Create<T>());
        }

        public ComponentDataFromEntity<T> GetComponentDataFromEntity<T>(bool isReadOnly = false) where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ComponentDataFromEntity<T>(typeIndex, m_Entities, m_JobSafetyManager.GetSafetyHandle(typeIndex, isReadOnly));
#else
            return new ComponentDataFromEntity<T>(typeIndex, m_Entities);
#endif
        }

        public FixedArrayFromEntity<T> GetFixedArrayFromEntity<T>(bool isReadOnly = false) where T : struct
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new FixedArrayFromEntity<T>(typeIndex, m_Entities, m_JobSafetyManager.GetSafetyHandle(typeIndex, isReadOnly));
#else
            return new FixedArrayFromEntity<T>(typeIndex, m_Entities);
#endif
        }

        unsafe public T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities.AssertEntityHasComponent(entity, typeIndex);
            m_JobSafetyManager.CompleteWriteDependency(typeIndex);

            byte* ptr = m_Entities.GetComponentDataWithType (entity, typeIndex);

            T value;
            UnsafeUtility.CopyPtrToStructure (ptr, out value);
            return value;
        }

        unsafe public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities.AssertEntityHasComponent(entity, typeIndex);

            m_JobSafetyManager.CompleteReadAndWriteDependency(typeIndex);

            byte* ptr = m_Entities.GetComponentDataWithType (entity, typeIndex);
            UnsafeUtility.CopyStructureToPtr (ref componentData, ptr);
        }

        internal unsafe void SetComponentObject(Entity entity, ComponentType componentType, object componentObject)
        {
            m_Entities.AssertEntityHasComponent(entity, componentType.typeIndex);

            Chunk* chunk;
            int chunkIndex;
            m_Entities.GetComponentChunk(entity, out chunk, out chunkIndex);
            m_ArchetypeManager.SetManagedObject(chunk, componentType, chunkIndex, componentObject);
        }

        public unsafe T GetComponentObject<T>(Entity entity) where T : Component
        {
            ComponentType componentType = ComponentType.Create<T>();
            m_Entities.AssertEntityHasComponent(entity, componentType.typeIndex);

            Chunk* chunk;
            int chunkIndex;
            m_Entities.GetComponentChunk(entity, out chunk, out chunkIndex);
            return m_ArchetypeManager.GetManagedObject(chunk, componentType, chunkIndex) as T;
        }

        public void GetAllUniqueSharedComponents(Type type, NativeList<int> componentIndices)
        {
            m_SharedComponentManager.GetAllUniqueSharedComponents(type, componentIndices);
        }

        unsafe public T GetSharedComponentData<T>(Entity entity) where T : struct, ISharedComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities.AssertEntityHasComponent(entity, typeIndex);
            var archetype = m_Entities.GetArchetype(entity);
            int indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            int sharedComponentIndex = m_Entities.GetSharedComponentDataIndex(entity, indexInTypeArray);
            return m_SharedComponentManager.GetSharedComponentData<T>(sharedComponentIndex);
        }

        //TODO: api?
        unsafe public T GetSharedComponentData<T>(int index) where T : struct, ISharedComponentData
        {
            return m_SharedComponentManager.GetSharedComponentData<T>(index);
        }


        unsafe public void AddSharedComponent<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            //TODO: optimize this (no need to move the entity to a new chunk twice)
            AddComponent(entity, ComponentType.Create<T>());
            SetSharedComponent(entity, componentData);
        }

        unsafe public void SetSharedComponent<T>(Entity entity, T componentData) where T: struct, ISharedComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities.AssertEntityHasComponent(entity, typeIndex);

            m_JobSafetyManager.CompleteReadAndWriteDependency(typeIndex);
            var archetype = m_Entities.GetArchetype(entity);
            int newSharedComponentDataIndex = m_SharedComponentManager.InsertSharedComponent(componentData);

            int indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);

            var srcChunk = m_Entities.GetComponentChunk(entity);
            int* srcSharedComponentValueArray = ArchetypeManager.GetSharedComponentValueArray(srcChunk);
            int sharedComponentOffset = archetype->sharedComponentOffset[indexInTypeArray];
            int oldSharedComponentDataIndex = srcSharedComponentValueArray[sharedComponentOffset];

            if (newSharedComponentDataIndex == oldSharedComponentDataIndex)
                return;

            var sharedComponentIndices = (int*)UnsafeUtility.Malloc(sizeof(int)*archetype->numSharedComponents, sizeof(int), Allocator.Temp);
            var srcSharedComponentDataIndices = ArchetypeManager.GetSharedComponentValueArray(srcChunk);

            ArchetypeManager.CopySharedComponentDataIndexArray(sharedComponentIndices, srcSharedComponentDataIndices, archetype->numSharedComponents);
            sharedComponentIndices[sharedComponentOffset] = newSharedComponentDataIndex;

            var newChunk = m_ArchetypeManager.GetChunkWithEmptySlots(archetype, sharedComponentIndices);
            int newChunkIndex = m_ArchetypeManager.AllocateIntoChunk(newChunk);

            m_Entities.MoveEntityToChunk(m_ArchetypeManager, entity, newChunk, newChunkIndex);

            UnsafeUtility.Free(sharedComponentIndices, Allocator.Temp);
        }


        unsafe public NativeArray<T> GetFixedArray<T>(Entity entity) where T : struct
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Entities.AssertEntityHasComponent(entity, typeIndex);
            if (TypeManager.GetComponentType<T>().category != TypeManager.TypeCategory.OtherValueType)
                throw new ArgumentException($"GetComponentFixedArray<{typeof(T)}> may not be IComponentData or ISharedComponentData");
#endif

            m_JobSafetyManager.CompleteWriteDependency(typeIndex);

            byte* ptr;
            int length;
            m_Entities.GetComponentDataWithTypeAndFixedArrayLength (entity, typeIndex, out ptr, out length);

            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, length, Allocator.Invalid);

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_JobSafetyManager.GetSafetyHandle(typeIndex, false));
            #endif

            return array;
        }

        public void CompleteAllJobs()
        {
            ComponentJobSafetyManager.CompleteAllJobsAndInvalidateArrays();
        }

        internal ComponentJobSafetyManager ComponentJobSafetyManager { get { return m_JobSafetyManager; } }
    }
}



