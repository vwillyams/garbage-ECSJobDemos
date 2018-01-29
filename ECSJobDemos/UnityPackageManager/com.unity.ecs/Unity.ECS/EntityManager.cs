﻿using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using Unity.Jobs;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace UnityEngine.ECS
{
    //@TODO: There is nothing prevent non-main thread (non-job thread) access of EntityMnaager.
    //       Static Analysis or runtime checks?

    //@TODO: safety?
    public unsafe struct EntityArchetype
    {
        [NativeDisableUnsafePtrRestriction]
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

    public unsafe class EntityManager : ScriptBehaviourManager
    {
        EntityDataManager*                m_Entities;

        ArchetypeManager                  m_ArchetypeManager;
        EntityGroupManager                m_GroupManager;
        ComponentJobSafetyManager         m_JobSafetyManager;

        static SharedComponentDataManager m_SharedComponentManager;

        EntityTransaction                 m_EntityTransaction;

        ComponentType*             m_CachedComponentTypeArray;
        ComponentTypeInArchetype*  m_CachedComponentTypeInArchetypeArray;

        protected sealed override void OnCreateManagerInternal(int capacity)
        {
        }

        protected override void OnCreateManager(int capacity)
        {
            m_Entities = (EntityDataManager*)UnsafeUtility.Malloc(sizeof(EntityDataManager), 64, Allocator.Persistent);
            m_Entities->OnCreate(capacity);

            if (m_SharedComponentManager == null)
                m_SharedComponentManager = new SharedComponentDataManager();
            m_SharedComponentManager.Retain();

            m_ArchetypeManager = new ArchetypeManager(m_SharedComponentManager);
            m_JobSafetyManager = new ComponentJobSafetyManager();
            m_GroupManager = new EntityGroupManager(m_JobSafetyManager);

            m_EntityTransaction = new EntityTransaction(m_ArchetypeManager, m_Entities);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_EntityTransaction.SetAtomicSafetyHandle(ComponentJobSafetyManager.CreationSafety);
#endif
            TypeManager.Initialize();

            m_CachedComponentTypeArray = (ComponentType*)UnsafeUtility.Malloc(sizeof(ComponentType) * 32 * 1024, 16, Allocator.Persistent);
            m_CachedComponentTypeInArchetypeArray = (ComponentTypeInArchetype*)UnsafeUtility.Malloc(sizeof(ComponentTypeInArchetype) * 32 * 1024, 16, Allocator.Persistent);
        }

        protected override void OnDestroyManager()
        {
            m_JobSafetyManager.Dispose(); m_JobSafetyManager = null;

            m_Entities->OnDestroy();
            UnsafeUtility.Free(m_Entities, Allocator.Persistent);
            m_Entities = null;
            m_ArchetypeManager.Dispose(); m_ArchetypeManager = null;
            m_GroupManager.Dispose(); m_GroupManager = null;
            m_EntityTransaction.OnDestroyManager();

            if (m_SharedComponentManager.Release())
                m_SharedComponentManager = null;

            UnsafeUtility.Free(m_CachedComponentTypeArray, Allocator.Persistent);
            m_CachedComponentTypeArray = null;

            UnsafeUtility.Free(m_CachedComponentTypeInArchetypeArray, Allocator.Persistent);
            m_CachedComponentTypeInArchetypeArray = null;
        }

        internal override void InternalUpdate()
        {
        }

        public bool IsCreated { get { return (m_CachedComponentTypeArray != null); } }

        public int EntityCapacity
        {
            get { return m_Entities->Capacity; }
            set
            {
                BeforeImmediateStructualChange();
                m_Entities->Capacity = value;
            }
        }

        int PopulatedCachedTypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeArray[0] = ComponentType.Create<Entity>();
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        int PopulatedCachedTypeInArchetypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeInArchetypeArray[0] = new ComponentTypeInArchetype(ComponentType.Create<Entity>());
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeInArchetypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        public ComponentGroup CreateComponentGroup(params ComponentType[] requiredComponents)
        {
            //@TODO: Better would be to seperate creation of archetypes and getting existing archetypes
            // and only flush when creating new ones...
            BeforeImmediateStructualChange();

            return m_GroupManager.CreateEntityGroup(m_ArchetypeManager, m_CachedComponentTypeArray, PopulatedCachedTypeArray(requiredComponents), new TransformAccessArray());
        }

        public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            int cachedComponentCount = PopulatedCachedTypeInArchetypeArray(types);
            
            // Lookup existing archetype (cheap)
            EntityArchetype entityArchetype;
            entityArchetype.archetype = m_ArchetypeManager.GetExistingArchetype(m_CachedComponentTypeInArchetypeArray, cachedComponentCount);
            if (entityArchetype.archetype != null)
                return entityArchetype;
            
            // Creating an archetype invalidates all iterators / jobs etc
            // because it affects the live iteration linked lists... 
            
            BeforeImmediateStructualChange();

            EntityArchetype type;
            type.archetype = m_ArchetypeManager.GetOrCreateArchetype(m_CachedComponentTypeInArchetypeArray, cachedComponentCount, m_GroupManager);
            return type;
        }

        public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            CreateEntityInternal(archetype, (Entity*)entities.GetUnsafePtr(), entities.Length);
        }

        public Entity CreateEntity(EntityArchetype archetype)
        {
            Entity entity;
            CreateEntityInternal(archetype, &entity, 1);
            return entity;
        }

        public Entity CreateEntity(params ComponentType[] types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        void CreateEntityInternal(EntityArchetype archetype, Entity* entities, int count)
        {
            BeforeImmediateStructualTransaction();

            m_Entities->CreateEntities(m_ArchetypeManager, archetype.archetype, entities, count, true);
            IncrementSharedComponentsVersion(entities[0]);

            AfterImmediateStructuralTransaction();
        }

        public void DestroyEntity(NativeArray<Entity> entities)
        {
            DestroyEntityInternal((Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        public void DestroyEntity(NativeSlice<Entity> entities)
        {
            DestroyEntityInternal((Entity*)entities.GetUnsafeReadOnlyPtr(), entities.Length);
        }

        public void DestroyEntity(Entity entity)
        {
            DestroyEntityInternal(&entity, 1);
        }

        void DestroyEntityInternal(Entity* entities, int count)
        {
            BeforeImmediateStructualChange();

            m_Entities->AssertEntitiesExist(entities, count);

            for (int i = 0; i < count; i++)
                IncrementSharedComponentsVersion(entities[i]);

            m_Entities->DeallocateEnties(m_ArchetypeManager, entities, count);
        }

        public bool Exists(Entity entity)
        {
            return m_Entities->Exists(entity);
        }

        public bool HasComponent<T>(Entity entity)
        {
            return m_Entities->HasComponent(entity, ComponentType.Create<T>());
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            return m_Entities->HasComponent(entity, type);
        }

        public Entity Instantiate(Entity srcEntity)
        {
            Entity entity;
            InstantiateInternal(srcEntity, &entity, 1);
            return entity;
        }

        public Entity Instantiate(GameObject srcGameObject)
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

        public void Instantiate(GameObject srcGameObject, NativeArray<Entity> outputEntities)
        {
            if (outputEntities.Length == 0)
                return;

            var entity = Instantiate(srcGameObject);
            outputEntities[0] = entity;

            Entity* entityPtr = (Entity*)outputEntities.GetUnsafePtr();
            InstantiateInternal(entity, entityPtr + 1, outputEntities.Length - 1);
        }

        public void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            InstantiateInternal(srcEntity, (Entity*)outputEntities.GetUnsafePtr(), outputEntities.Length);
        }


        void InstantiateInternal(Entity srcEntity, Entity* outputEntities, int count)
        {
            BeforeImmediateStructualTransaction();

            if (!m_Entities->Exists(srcEntity))
                throw new System.ArgumentException("srcEntity is not a valid entity");

            m_Entities->InstantiateEntities(m_ArchetypeManager, srcEntity, outputEntities, count, true);
            IncrementSharedComponentsVersion(srcEntity);

            AfterImmediateStructuralTransaction();
        }

        public void AddComponent(Entity entity, ComponentType type)
        {
            BeforeImmediateStructualChange();
            IncrementSharedComponentsVersion(entity);

            m_Entities->AssertEntitiesExist(&entity, 1);

            var componentType = new ComponentTypeInArchetype(type);
            Archetype* archetype = m_Entities->GetArchetype(entity);
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
            Archetype* newType = m_ArchetypeManager.GetOrCreateArchetype(m_CachedComponentTypeInArchetypeArray, archetype->typesCount + 1, m_GroupManager);

            int* sharedComponentDataIndices = null;
            if (newType->numSharedComponents > 0)
            {
                var oldSharedComponentDataIndices = m_Entities->GetComponentChunk(entity)->GetSharedComponentValueArray();
                var newComponentIsShared = (TypeManager.TypeCategory.ISharedComponentData == TypeManager.GetComponentType(type.typeIndex).category);
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
                        int sharedIndex = 0;
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
                                sharedComponentDataIndices[sharedIndex + 1] = oldSharedComponentDataIndices[sharedIndex];
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

            m_Entities->SetArchetype(m_ArchetypeManager, entity, newType, sharedComponentDataIndices);
        }

        private void IncrementSharedComponentsVersion(Entity entity)
        {
            Archetype* archtype = m_Entities->GetArchetype(entity);
            var sharedComponentDataIndices = m_Entities->GetComponentChunk(entity)->GetSharedComponentValueArray();
            for (int i = 0; i < archtype->numSharedComponents; i++)
            {
                m_SharedComponentManager.IncrementSharedComponentVersion(sharedComponentDataIndices[i]);
            }
        }
        
        public void RemoveComponent(Entity entity, ComponentType type)
        {
            BeforeImmediateStructualChange();
            IncrementSharedComponentsVersion(entity);

            var componentType = new ComponentTypeInArchetype(type);

            m_Entities->AssertEntityHasComponent(entity, type);
            
            Archetype* archtype = m_Entities->GetArchetype(entity);
            int removedTypes = 0;
            for (int t = 0; t < archtype->typesCount; ++t)
            {
                if (archtype->types[t].typeIndex == componentType.typeIndex)
                    ++removedTypes;
                else
                    m_CachedComponentTypeInArchetypeArray[t - removedTypes] = archtype->types[t];
            }
            

            Assertions.Assert.AreNotEqual(-1, removedTypes);

            Archetype* newType = m_ArchetypeManager.GetOrCreateArchetype(m_CachedComponentTypeInArchetypeArray, archtype->typesCount - removedTypes, m_GroupManager);

            int* sharedComponentDataIndices = null;
            if (newType->numSharedComponents > 0)
            {
                var oldSharedComponentDataIndices = m_Entities->GetComponentChunk(entity)->GetSharedComponentValueArray();
                bool removedComponentIsShared = (TypeManager.TypeCategory.ISharedComponentData == TypeManager.GetComponentType(type.typeIndex).category);
                removedTypes = 0;
                if (removedComponentIsShared)
                {
                    int* tempAlloc = stackalloc int[newType->numSharedComponents];
                    sharedComponentDataIndices = tempAlloc;
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

            m_Entities->SetArchetype(m_ArchetypeManager, entity, newType, sharedComponentDataIndices);
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

        public T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);
            m_JobSafetyManager.CompleteWriteDependency(typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);

            T value;
            UnsafeUtility.CopyPtrToStructure (ptr, out value);
            return value;
        }

        public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);

            m_JobSafetyManager.CompleteReadAndWriteDependency(typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);
            UnsafeUtility.CopyStructureToPtr (ref componentData, ptr);
        }

        internal void SetComponentObject(Entity entity, ComponentType componentType, object componentObject)
        {
            m_Entities->AssertEntityHasComponent(entity, componentType.typeIndex);

            Chunk* chunk;
            int chunkIndex;
            m_Entities->GetComponentChunk(entity, out chunk, out chunkIndex);
            m_ArchetypeManager.SetManagedObject(chunk, componentType, chunkIndex, componentObject);
        }

        public T GetComponentObject<T>(Entity entity) where T : Component
        {
            ComponentType componentType = ComponentType.Create<T>();
            m_Entities->AssertEntityHasComponent(entity, componentType.typeIndex);

            Chunk* chunk;
            int chunkIndex;
            m_Entities->GetComponentChunk(entity, out chunk, out chunkIndex);
            return m_ArchetypeManager.GetManagedObject(chunk, componentType, chunkIndex) as T;
        }

        public void GetAllUniqueSharedComponents<T>(System.Collections.Generic.List<T> sharedComponentValues)
            where T : struct, ISharedComponentData
        {
            m_SharedComponentManager.GetAllUniqueSharedComponents(sharedComponentValues);
        }

        unsafe public T GetSharedComponent<T>(Entity entity) where T : struct, ISharedComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);

            int sharedComponentIndex = m_Entities->GetSharedComponentDataIndex(entity, typeIndex);
            return m_SharedComponentManager.GetSharedComponentData<T>(sharedComponentIndex);
        }

        public void AddSharedComponent<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            //TODO: optimize this (no need to move the entity to a new chunk twice)
            AddComponent(entity, ComponentType.Create<T>());
            SetSharedComponent(entity, componentData);
        }

        public void RemoveSharedComponent<T>(Entity entity) where T : struct, ISharedComponentData
        {
            RemoveComponent(entity, ComponentType.Create<T>());
        }

        unsafe public void SetSharedComponent<T>(Entity entity, T componentData) where T: struct, ISharedComponentData
        {
            BeforeImmediateStructualChange();
            
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);
            
            int newSharedComponentDataIndex = m_SharedComponentManager.InsertSharedComponent(componentData);
            m_Entities->SetSharedComponentDataIndex(m_ArchetypeManager, entity, typeIndex, newSharedComponentDataIndex);
            m_SharedComponentManager.RemoveReference(newSharedComponentDataIndex);
        }

        unsafe public NativeArray<T> GetFixedArray<T>(Entity entity) where T : struct
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Entities->AssertEntityHasComponent(entity, typeIndex);
            if (TypeManager.GetComponentType<T>().category != TypeManager.TypeCategory.OtherValueType)
                throw new ArgumentException($"GetComponentFixedArray<{typeof(T)}> may not be IComponentData or ISharedComponentData");
#endif

            m_JobSafetyManager.CompleteWriteDependency(typeIndex);

            byte* ptr;
            int length;
            m_Entities->GetComponentDataWithTypeAndFixedArrayLength (entity, typeIndex, out ptr, out length);

            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, length, Allocator.Invalid);

            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_JobSafetyManager.GetSafetyHandle(typeIndex, false));
            #endif

            return array;
        }

        internal ComponentJobSafetyManager ComponentJobSafetyManager { get { return m_JobSafetyManager; } }

        public EntityTransaction BeginTransaction()
        {
            return m_EntityTransaction;
        }

        public JobHandle EntityTransactionDependency
        {
            get { return m_JobSafetyManager.CreationJob; }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(m_JobSafetyManager.CreationJob, value))
                    throw new System.ArgumentException("The assigned job dependency does not depend on the previous EntityTransactionDependency. You must schedule all jobs using EntityTransaction with a dependency on EntityManager.EntityTransactionDependency.");
#endif

                m_JobSafetyManager.CreationJob = value;
            }
        }

        // Call this transactionable structure changes (instantiate, create entity etc)
        void BeforeImmediateStructualTransaction()
        {
            CommitTransaction();
        }
        // Call this after transactionable structure changes to integrate them
        void AfterImmediateStructuralTransaction()
        {
            m_ArchetypeManager.IntegrateChunks();
        }
        // Call this before doing a change that has to be applied immediately anyway.
        // Thus no need to do anything after the transaction
        void BeforeImmediateStructualChange()
        {
            CommitTransaction();
        }

        public void CommitTransaction()
        {
            // We are going to mutate ComponentGroup iteration state, so no iteration jobs may be running in parallel to this
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();
            // Creation is exclusive to one job or main thread at a time, thus make sure any creation jobs are done
            m_JobSafetyManager.CompleteCreationJob();
            // Ensure that all transaction state has been applied
            m_ArchetypeManager.IntegrateChunks();
        }

        public void CompleteAllJobs()
        {
            CommitTransaction();
        }

        public void MoveEntitiesFrom(EntityManager srcEntities)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (srcEntities == this)
                throw new System.ArgumentException("srcEntities must not be the same as this EntityManager.");
#endif

            BeforeImmediateStructualChange();
            srcEntities.BeforeImmediateStructualChange();
            
            ArchetypeManager.MoveChunks(srcEntities.m_ArchetypeManager, srcEntities.m_Entities, m_ArchetypeManager, m_GroupManager, m_SharedComponentManager, m_Entities);
        }

        public void CheckInternalConsistency()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            CommitTransaction();

            //@TODO: Validate from perspective of componentgroup...
            //@TODO: Validate shared component data refcounts...
            int entityCountEntityData = m_Entities->CheckInternalConsistency();
            int entityCountArchetypeManager = m_ArchetypeManager.CheckInternalConsistency();

            Assert.AreEqual(entityCountEntityData, entityCountArchetypeManager);
#endif
        }
    }
}



