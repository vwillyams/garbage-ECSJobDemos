using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using System;

namespace UnityEngine.ECS
{
    //@TODO: safety?
    public unsafe struct EntityArchetype
    {
        public Archetype* archetype;
    }

    public struct Entity
    {
        public int index;
        public int version;

        //@TODO: Manager index for debugging?
    }

    public class EntityManager : ScriptBehaviourManager
    {
        EntityDataManager           m_Entities;

        TypeManager                 m_TypeManager;
        EntityGroupManager          m_GroupManager;
        ComponentJobSafetyManager   m_JobSafetyManager;

        unsafe int*                 m_CachedIntArray;

        unsafe protected override void OnCreateManager (int capacity)
        {
            base.OnCreateManager (capacity);

            m_Entities.OnCreate ();
            m_TypeManager = new TypeManager();
            m_JobSafetyManager = new ComponentJobSafetyManager();
            m_GroupManager = new EntityGroupManager(m_JobSafetyManager);
            RealTypeManager.Initialize ();

            m_CachedIntArray = (int*)UnsafeUtility.Malloc (sizeof(int) * 32 * 1024, 16, Allocator.Persistent);
        }

        unsafe protected override void OnDestroyManager ()
        {
            base.OnDestroyManager ();

            m_JobSafetyManager.Dispose(); m_JobSafetyManager = null;

            m_Entities.OnDestroy ();
            m_TypeManager.Dispose (); m_TypeManager = null;
            m_GroupManager.Dispose (); m_GroupManager = null;

            UnsafeUtility.Free ((IntPtr)m_CachedIntArray, Allocator.Persistent);
            m_CachedIntArray = (int*)IntPtr.Zero;
        }

        unsafe public bool IsCreated {get{return (m_CachedIntArray != null);}}

        unsafe public int PopulatedCachedTypeArray(ComponentType[] requiredComponents)
        {
            m_CachedIntArray[0] = RealTypeManager.GetTypeIndex<Entity>();
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedIntArray, i + 1, requiredComponents[i].typeIndex);
            return requiredComponents.Length + 1;
        }

        unsafe public EntityGroup CreateEntityGroup(params ComponentType[] requiredComponents)
        {
            return m_GroupManager.CreateEntityGroup(m_TypeManager, m_CachedIntArray, PopulatedCachedTypeArray(requiredComponents));
        }

        unsafe public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            EntityArchetype type;
            type.archetype = m_TypeManager.GetArchetype(m_CachedIntArray, PopulatedCachedTypeArray(types), m_GroupManager);
            return type;
        }

        unsafe public EntityArchetype CreateArchetype(ComponentType[] types, int[] strideGroup)
        {
            EntityArchetype type;
            type.archetype = m_TypeManager.GetArchetype(m_CachedIntArray, PopulatedCachedTypeArray(types), m_GroupManager);
            return type;
        }



        unsafe public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            CreateEntityInternal (archetype, (Entity*)entities.UnsafePtr, entities.Length);
        }

        unsafe public Entity CreateEntity(EntityArchetype archetype)
        {
            Entity entity;
            CreateEntityInternal (archetype, &entity, 1);
            return entity;
        }

        unsafe public Entity CreateEntity(params ComponentType[] types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        unsafe void CreateEntityInternal(EntityArchetype archetype, Entity* entities, int count)
        {
            m_JobSafetyManager.InvalidateAll();

            while (count != 0)
            {
                Chunk* chunk = m_TypeManager.GetChunkWithEmptySlots (archetype.archetype);
                int allocatedIndex;
                int allocatedCount = TypeManager.AllocateIntoChunk (chunk, count, out allocatedIndex);
                m_Entities.AllocateEntities(archetype.archetype, chunk, allocatedIndex, allocatedCount, entities);
                ChunkDataUtility.ClearComponents(chunk, allocatedIndex, allocatedCount);

                entities += allocatedCount;
                count -= allocatedCount;
            }
        }

        unsafe public void Destroy (NativeArray<Entity> entities)
        {
            m_JobSafetyManager.InvalidateAll();

            m_Entities.DeallocateEnties((Entity*)entities.UnsafeReadOnlyPtr, entities.Length);
        }

        unsafe public void Destroy(Entity entity)
        {
            m_JobSafetyManager.InvalidateAll();

            m_Entities.DeallocateEnties(&entity, 1);
        }

        unsafe public bool Exists(Entity entity)
        {
            return m_Entities.Exists (entity);
        }

        public bool HasComponent<T>(Entity entity) where T : IComponentData
        {
            return m_Entities.HasComponent (entity, RealTypeManager.GetTypeIndex<T>());
        }

        public bool HasComponent(Entity entity, ComponentType type)
        {
            return m_Entities.HasComponent (entity, type.typeIndex);
        }

        public unsafe Entity Instantiate(Entity srcEntity)
        {
            Entity entity;
            Instantiate(srcEntity, &entity, 1);
            return entity;
        }

        public unsafe void Instantiate(GameObject srcGameObject, NativeArray<Entity> outputEntities)
        {
            var components = srcGameObject.GetComponents<ComponentDataWrapperBase> ();
            var componentTypes = new ComponentType[components.Length];
            for (int t = 0; t != components.Length; ++t)
                componentTypes[t] = new ComponentType(components[t].GetIComponentDataType());
            var srcEntity = CreateEntity(componentTypes);
            for (int t = 0; t != components.Length; ++t)
                components[t].UpdateComponentData(this, srcEntity);
            Instantiate(srcEntity, (Entity*)outputEntities.UnsafePtr, outputEntities.Length);
            Destroy(srcEntity);
        }
        public unsafe void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            Instantiate(srcEntity, (Entity*)outputEntities.UnsafePtr, outputEntities.Length);
        }

        public unsafe void Instantiate(Entity srcEntity, Entity* outputEntities, int count)
        {
            m_JobSafetyManager.InvalidateAll();

            if (!m_Entities.Exists(srcEntity))
                throw new System.ArgumentException("srcEntity is not a valid entity");


            int srcIndex = m_Entities.m_Entities[srcEntity.index].index;
            Chunk* srcChunk = m_Entities.m_Entities[srcEntity.index].chunk;
            Archetype* srcArchetype = m_Entities.m_Entities[srcEntity.index].archetype;

            while (count != 0)
            {
                Chunk* chunk = m_TypeManager.GetChunkWithEmptySlots(srcArchetype);
                int indexInChunk;
                int allocatedCount = TypeManager.AllocateIntoChunk(chunk, count, out indexInChunk);

                ChunkDataUtility.ReplicateComponents(srcChunk, srcIndex, chunk, indexInChunk, allocatedCount);

                m_Entities.AllocateEntities(srcArchetype, chunk, indexInChunk, allocatedCount, outputEntities);

                outputEntities += allocatedCount;
                count -= allocatedCount;
            }
        }

        public unsafe void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            m_JobSafetyManager.InvalidateAll();

            int typeIndex = RealTypeManager.GetTypeIndex<T>();
            Archetype* type = m_Entities.GetArchetype(entity);
            int t = 0;
            while (t < type->typesCount && type->types[t] < typeIndex)
            {
                m_CachedIntArray[t] = type->types[t];
                ++t;
            }
            if (t < type->typesCount && type->types[t] == typeIndex)
                throw new InvalidOperationException("Trying to add a component to an entity which is already present");
            m_CachedIntArray[t] = typeIndex;
            while (t < type->typesCount)
            {
                m_CachedIntArray[t+1] = type->types[t];
                ++t;
            }
            Archetype* newType = m_TypeManager.GetArchetype(m_CachedIntArray, type->typesCount + 1, m_GroupManager);
            Chunk* newChunk = m_TypeManager.GetChunkWithEmptySlots(newType);

            int newChunkIndex = TypeManager.AllocateIntoChunk(newChunk);
            m_Entities.SetArchetype(entity, newType, newChunk, newChunkIndex);
            SetComponent<T>(entity, componentData);
        }

        public unsafe void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
        {
            m_JobSafetyManager.InvalidateAll();

            int typeIndex = RealTypeManager.GetTypeIndex<T>();
            Archetype* type = m_Entities.GetArchetype(entity);
            int removedTypes = 0;
            for (int t = 0; t < type->typesCount; ++t)
            {
                if (type->types[t] == typeIndex)
                    ++removedTypes;
                else
                    m_CachedIntArray[t-removedTypes] = type->types[t];
            }
            if (removedTypes != 1)
                throw new InvalidOperationException("Trying to remove a component from an entity which is not present");
            Archetype* newType = m_TypeManager.GetArchetype(m_CachedIntArray, type->typesCount - removedTypes, m_GroupManager);
            Chunk* newChunk = m_TypeManager.GetChunkWithEmptySlots(newType);
            int newChunkIndex = TypeManager.AllocateIntoChunk(newChunk);
            m_Entities.SetArchetype(entity, newType, newChunk, newChunkIndex);
        }

        public T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            IntPtr ptr = m_Entities.GetComponentDataWithType (entity, RealTypeManager.GetTypeIndex<T>());

            T value;
            UnsafeUtility.CopyPtrToStructure (ptr, out value);
            return value;
        }

        public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            IntPtr ptr = m_Entities.GetComponentDataWithType (entity, RealTypeManager.GetTypeIndex<T>());
            UnsafeUtility.CopyStructureToPtr (ref componentData, ptr);
        }

        internal void SetComponentInstanceID(Entity entity, ComponentType componentType, int componentInstanceID)
        {
            IntPtr ptr = m_Entities.GetComponentDataWithType(entity, componentType.typeIndex);
            UnsafeUtility.CopyStructureToPtr(ref componentInstanceID, ptr);
        }


        internal ComponentJobSafetyManager ComponentJobSafetyManager { get { return m_JobSafetyManager; } }
    }
}



