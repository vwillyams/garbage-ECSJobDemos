﻿using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Collections.Generic;
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

        public bool Valid => archetype != null; 

        public static bool operator ==(EntityArchetype lhs, EntityArchetype rhs) { return lhs.archetype == rhs.archetype; }
        public static bool operator !=(EntityArchetype lhs, EntityArchetype rhs) { return lhs.archetype != rhs.archetype; }
        public override bool Equals(object compare) { return this == (EntityArchetype)compare; }
        public override int GetHashCode() { return (int)archetype; }
    }


    public struct Entity : IEquatable<Entity>
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

        public bool Equals(Entity entity)
        {
            return (entity.index == index) && (entity.version == version);
        }
    }

    public sealed unsafe class EntityManager : ScriptBehaviourManager
    {
        EntityDataManager*                m_Entities;

        ArchetypeManager                  m_ArchetypeManager;
        EntityGroupManager                m_GroupManager;
        ComponentJobSafetyManager         m_JobSafetyManager;

        static SharedComponentDataManager m_SharedComponentManager;

        EntityTransaction                 m_EntityTransaction;

        ComponentType*                    m_CachedComponentTypeArray;
        ComponentTypeInArchetype*         m_CachedComponentTypeInArchetypeArray;

        protected override void OnCreateManagerInternal(World world, int capacity) { }
        protected override void OnBeforeDestroyManagerInternal() { }
        protected override void OnAfterDestroyManagerInternal() { }

        protected override void OnCreateManager(int capacity)
        {
            TypeManager.Initialize();

            m_Entities = (EntityDataManager*)UnsafeUtility.Malloc(sizeof(EntityDataManager), 64, Allocator.Persistent);
            m_Entities->OnCreate(capacity);

            if (m_SharedComponentManager == null)
                m_SharedComponentManager = new SharedComponentDataManager();
            m_SharedComponentManager.Retain();

            m_ArchetypeManager = new ArchetypeManager(m_SharedComponentManager);
            m_JobSafetyManager = new ComponentJobSafetyManager();
            m_GroupManager = new EntityGroupManager(m_JobSafetyManager);

            m_EntityTransaction = new EntityTransaction(m_ArchetypeManager, m_GroupManager, m_SharedComponentManager, m_Entities);

            m_CachedComponentTypeArray = (ComponentType*)UnsafeUtility.Malloc(sizeof(ComponentType) * 32 * 1024, 16, Allocator.Persistent);
            m_CachedComponentTypeInArchetypeArray = (ComponentTypeInArchetype*)UnsafeUtility.Malloc(sizeof(ComponentTypeInArchetype) * 32 * 1024, 16, Allocator.Persistent);
        }

        protected override void OnDestroyManager()
        {
            EndTransaction();
            
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
                BeforeStructuralChange();
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
            BeforeStructuralChange();
            
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
            BeforeStructuralChange();

            entityArchetype.archetype = m_ArchetypeManager.GetOrCreateArchetype(m_CachedComponentTypeInArchetypeArray, cachedComponentCount, m_GroupManager);
            return entityArchetype;
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
            BeforeStructuralChange();
            m_Entities->CreateEntities(m_ArchetypeManager, archetype.archetype, entities, count);
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
            BeforeStructuralChange();
            m_Entities->AssertEntitiesExist(entities, count);
            m_Entities->DeallocateEnties(m_ArchetypeManager, m_SharedComponentManager, entities, count);
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
            BeforeStructuralChange();
            if (!m_Entities->Exists(srcEntity))
                throw new System.ArgumentException("srcEntity is not a valid entity");

            m_Entities->InstantiateEntities(m_ArchetypeManager, m_SharedComponentManager, srcEntity, outputEntities, count);
        }

        public void AddComponent(Entity entity, ComponentType type)
        {
            BeforeStructuralChange();
            m_Entities->AssertEntitiesExist(&entity, 1);
            m_Entities->AddComponent(entity, type, m_ArchetypeManager, m_SharedComponentManager, m_GroupManager, m_CachedComponentTypeInArchetypeArray);
        }
        
        public void RemoveComponent(Entity entity, ComponentType type)
        {
            BeforeStructuralChange();
            m_Entities->AssertEntityHasComponent(entity, type);
            m_Entities->RemoveComponent(entity, type, m_ArchetypeManager, m_SharedComponentManager, m_GroupManager, m_CachedComponentTypeInArchetypeArray);
        }
        
        public void RemoveComponent<T>(Entity entity)
        {
            RemoveComponent(entity, ComponentType.Create<T>());
        }

        public void AddComponentData<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            AddComponent(entity, ComponentType.Create<T>());
            SetComponentData<T>(entity, componentData);
        }

        internal ComponentDataFromEntity<T> GetComponentDataFromEntity<T>(int typeIndex, bool isReadOnly) where T : struct, IComponentData
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ComponentDataFromEntity<T>(typeIndex, m_Entities, m_JobSafetyManager.GetSafetyHandle(typeIndex, isReadOnly));
#else
            return new ComponentDataFromEntity<T>(typeIndex, m_Entities);
#endif
        }
        
        public ComponentDataFromEntity<T> GetComponentDataFromEntity<T>(bool isReadOnly = false) where T : struct, IComponentData
        {
            return GetComponentDataFromEntity<T>(TypeManager.GetTypeIndex<T>(), isReadOnly);
        }

        public FixedArrayFromEntity<T> GetFixedArrayFromEntity<T>(int typeIndex, bool isReadOnly = false) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new FixedArrayFromEntity<T>(typeIndex, m_Entities, m_JobSafetyManager.GetSafetyHandle(typeIndex, isReadOnly));
#else
            return new FixedArrayFromEntity<T>(typeIndex, m_Entities);
#endif
        }

        public FixedArrayFromEntity<T> GetFixedArrayFromEntity<T>(bool isReadOnly = false) where T : struct
        {
            return GetFixedArrayFromEntity<T>(TypeManager.GetTypeIndex<T>(), isReadOnly);
        }

        public T GetComponentData<T>(Entity entity) where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);
            m_JobSafetyManager.CompleteWriteDependency(typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);

            T value;
            UnsafeUtility.CopyPtrToStructure (ptr, out value);
            return value;
        }

        public void SetComponentData<T>(Entity entity, T componentData) where T: struct, IComponentData
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

        public void GetAllUniqueSharedComponentDatas<T>(System.Collections.Generic.List<T> sharedComponentValues)
            where T : struct, ISharedComponentData
        {
            m_SharedComponentManager.GetAllUniqueSharedComponents(sharedComponentValues);
        }

        unsafe public T GetSharedComponentData<T>(Entity entity) where T : struct, ISharedComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);

            int sharedComponentIndex = m_Entities->GetSharedComponentDataIndex(entity, typeIndex);
            return m_SharedComponentManager.GetSharedComponentData<T>(sharedComponentIndex);
        }

        public void AddSharedComponentData<T>(Entity entity, T componentData) where T : struct, ISharedComponentData
        {
            //TODO: optimize this (no need to move the entity to a new chunk twice)
            AddComponent(entity, ComponentType.Create<T>());
            SetSharedComponentData(entity, componentData);
        }

        public void SetSharedComponentData<T>(Entity entity, T componentData) where T: struct, ISharedComponentData
        {
            BeforeStructuralChange();
            
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);
                        
            int newSharedComponentDataIndex = m_SharedComponentManager.InsertSharedComponent(componentData);
            m_Entities->SetSharedComponentDataIndex(m_ArchetypeManager, m_SharedComponentManager, entity, typeIndex, newSharedComponentDataIndex);
            m_SharedComponentManager.RemoveReference(newSharedComponentDataIndex);
        }

        public NativeArray<T> GetFixedArray<T>(Entity entity) where T : struct
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

        public NativeArray<Entity> GetAllEntities(Allocator allocator = Allocator.Temp)
        {
            var entityGroup = CreateComponentGroup();
            var groupArray = entityGroup.GetEntityArray();
            
            var array = new NativeArray<Entity>(groupArray.Length, allocator);
            groupArray.CopyTo(array);
            return array;
        }

        unsafe public NativeArray<ComponentType> GetComponentTypes(Entity entity, Allocator allocator = Allocator.Temp)
        {
            m_Entities->AssertEntitiesExist(&entity, 1);
            
            Archetype* archetype = m_Entities->GetArchetype(entity);

            var components = new NativeArray<ComponentType>(archetype->typesCount - 1, allocator);

            for (int i = 1; i < archetype->typesCount;i++)
                components[i-1] = archetype->types[i].ToComponentType();

            return components;
        }
        
        public int GetComponentCount(Entity entity)
        {
            m_Entities->AssertEntitiesExist(&entity, 1);
            Archetype* archetype = m_Entities->GetArchetype(entity);
            return archetype->typesCount - 1;
        }

        public int GetComponentTypeIndex(Entity entity, int index)
        {
            m_Entities->AssertEntitiesExist(&entity, 1);
            Archetype* archetype = m_Entities->GetArchetype(entity);

            if ((uint) index >= archetype->typesCount)
            {
                return -1;
            }

            return archetype->types[index + 1].typeIndex;
        }

        public void SetComponentDataRaw(Entity entity, int typeIndex, void* data, int size)
        {
            m_Entities->AssertEntityHasComponent(entity, typeIndex);

            m_JobSafetyManager.CompleteReadAndWriteDependency(typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);
            UnsafeUtility.MemCpy(ptr, data, size);
        }
        
        public void* GetComponentDataRaw(Entity entity, int typeIndex)
        {
            m_Entities->AssertEntityHasComponent(entity, typeIndex);

            m_JobSafetyManager.CompleteReadAndWriteDependency(typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);
            return ptr;
        }
        
        public object GetComponentBoxed(Entity entity, ComponentType componentType)
        {
            var ptr = GetComponentDataRaw(entity, componentType.typeIndex);

            var type = TypeManager.GetType(componentType.typeIndex);
            var boxed = Activator.CreateInstance(type);

            ulong gcHandle;
            byte* boxedPtr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(boxed, out gcHandle);
            //@TODO: harcoded object class sizeof hack
            UnsafeUtility.MemCpy(boxedPtr + 16, ptr, UnsafeUtility.SizeOf(type));
            
            UnsafeUtility.ReleaseGCObject(gcHandle);

            return boxed;
        }
        
        public void SetComponentBoxed(Entity entity, ComponentType componentType, object boxedObject)
        {
            var type = TypeManager.GetType(componentType.typeIndex);

            ulong gcHandle;
            byte* boxedPtr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(boxedObject, out gcHandle);
            //@TODO: harcoded object class sizeof hack
            SetComponentDataRaw(entity, componentType.typeIndex, boxedPtr + 16, UnsafeUtility.SizeOf(type));
            
            UnsafeUtility.ReleaseGCObject(gcHandle);
        }
        
        public int GetComponentOrderVersion<T>()
        {
            return m_Entities->GetComponentTypeOrderVersion(TypeManager.GetTypeIndex<T>());
        }
        
        public int GetSharedComponentOrderVersion<T>(T sharedComponent) where T : struct, ISharedComponentData
        {
            return m_SharedComponentManager.GetSharedComponentVersion(sharedComponent);
        }
        
        internal ComponentJobSafetyManager ComponentJobSafetyManager { get { return m_JobSafetyManager; } }

        public EntityTransaction BeginTransaction()
        {
            m_JobSafetyManager.BeginTransaction();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_EntityTransaction.SetAtomicSafetyHandle(m_JobSafetyManager.ExclusiveTransactionSafety);
#endif
            return m_EntityTransaction;
        }

        public JobHandle EntityTransactionDependency
        {
            get { return m_JobSafetyManager.ExclusiveTransactionDependency; }
            set { m_JobSafetyManager.ExclusiveTransactionDependency = value; }
        }

        public void EndTransaction()
        {
            m_JobSafetyManager.EndTransaction();
        }

        void BeforeStructuralChange()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_JobSafetyManager.IsInTransaction)
                throw new System.InvalidOperationException("Access to EntityManager is not allowed after EntityManager.BeginTransaction(); has been called.");
#endif
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();
        }

        //@TODO: Not clear to me what this method is really for...
        public void CompleteAllJobs()
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();
        }

        public void MoveEntitiesFrom(EntityManager srcEntities)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (srcEntities == this)
                throw new System.ArgumentException("srcEntities must not be the same as this EntityManager.");
#endif

            BeforeStructuralChange();
            srcEntities.BeforeStructuralChange();

            ArchetypeManager.MoveChunks(srcEntities.m_ArchetypeManager, srcEntities.m_Entities, m_ArchetypeManager, m_GroupManager, m_SharedComponentManager, m_Entities);
            
            //@TODO: Need to incrmeent the component versions based the moved chunks...
        }

        public void CheckInternalConsistency()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            //@TODO: Validate from perspective of componentgroup...
            //@TODO: Validate shared component data refcounts...
            int entityCountEntityData = m_Entities->CheckInternalConsistency();
            int entityCountArchetypeManager = m_ArchetypeManager.CheckInternalConsistency();

            Assert.AreEqual(entityCountEntityData, entityCountArchetypeManager);
#endif
        }

        public List<Type> GetAssignableComponentTypes(Type interfaceType)
        {
            // #todo Cache this. It only can change when TypeManager.GetTypeCount() changes
            int componentTypeCount = TypeManager.GetTypeCount();
            var assignableTypes = new List<Type>();
            for (int i = 0; i < componentTypeCount; i++)
            {
              Type type = TypeManager.GetType(i);
              if (interfaceType.IsAssignableFrom(type))
              {
                  assignableTypes.Add(type);
              }
            }
            return assignableTypes;
        }
    }
}
