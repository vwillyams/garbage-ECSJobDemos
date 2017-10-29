using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;

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

        //@TODO: Manager index for debugging?
    }

    public class EntityManager : ScriptBehaviourManager
    {
        EntityDataManager m_Entities;

        ArchetypeManager m_ArchetypeManager;
        EntityGroupManager m_GroupManager;
        ComponentJobSafetyManager m_JobSafetyManager;

        SharedComponentDataManager m_SharedComponentManager;

        unsafe ComponentType* m_CachedComponentTypeArray;

        unsafe protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            m_Entities.OnCreate();
            m_ArchetypeManager = new ArchetypeManager();
            m_JobSafetyManager = new ComponentJobSafetyManager();
            m_GroupManager = new EntityGroupManager(m_JobSafetyManager);
            m_SharedComponentManager = new SharedComponentDataManager();
            TypeManager.Initialize();

            m_CachedComponentTypeArray = (ComponentType*)UnsafeUtility.Malloc(sizeof(int) * 32 * 1024, 16, Allocator.Persistent);
        }

        unsafe protected override void OnDestroyManager()
        {
            base.OnDestroyManager();

            m_JobSafetyManager.Dispose(); m_JobSafetyManager = null;
            m_SharedComponentManager.Dispose(); m_SharedComponentManager = null;
            m_Entities.OnDestroy();
            m_ArchetypeManager.Dispose(); m_ArchetypeManager = null;
            m_GroupManager.Dispose(); m_GroupManager = null;

            UnsafeUtility.Free((IntPtr)m_CachedComponentTypeArray, Allocator.Persistent);
            m_CachedComponentTypeArray = null;
        }

        unsafe public bool IsCreated { get { return (m_CachedComponentTypeArray != null); } }

        unsafe int PopulatedCachedTypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeArray[0] = ComponentType.Create<Entity>();
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        unsafe public ComponentGroup CreateComponentGroup(params ComponentType[] requiredComponents)
        {
            return m_GroupManager.CreateEntityGroup(m_ArchetypeManager, m_CachedComponentTypeArray, PopulatedCachedTypeArray(requiredComponents), new TransformAccessArray());
        }
        unsafe public ComponentGroup CreateComponentGroup(UnityEngine.Jobs.TransformAccessArray trans, params ComponentType[] requiredComponents)
        {
            int len = PopulatedCachedTypeArray(requiredComponents);
            if (trans.IsCreated)
            {
                bool hasTransform = false;
                var transformType = ComponentType.Create<Transform>();
                for (int i = 0; i < len; ++i)
                {
                    if (m_CachedComponentTypeArray[i] == transformType)
                        hasTransform = true;
                }
                if (!hasTransform)
                {
                    SortingUtilities.InsertSorted(m_CachedComponentTypeArray, len, transformType);
                    ++len;
                }
            }

            return m_GroupManager.CreateEntityGroup(m_ArchetypeManager, m_CachedComponentTypeArray, len, trans);
        }

        unsafe public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            EntityArchetype type;
            type.archetype = m_ArchetypeManager.GetArchetype(m_CachedComponentTypeArray, PopulatedCachedTypeArray(types), m_GroupManager, m_SharedComponentManager);
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
                Chunk* chunk = m_ArchetypeManager.GetChunkWithEmptySlots(archetype.archetype);
                int allocatedIndex;
                int allocatedCount = ArchetypeManager.AllocateIntoChunk(chunk, count, out allocatedIndex);
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

        unsafe public bool Exists(Entity entity)
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

            while (count != 0)
            {
                Chunk* chunk = m_ArchetypeManager.GetChunkWithEmptySlots(srcArchetype);
                int indexInChunk;
                int allocatedCount = ArchetypeManager.AllocateIntoChunk(chunk, count, out indexInChunk);

                ChunkDataUtility.ReplicateComponents(srcChunk, srcIndex, chunk, indexInChunk, allocatedCount);

                m_Entities.AllocateEntities(srcArchetype, chunk, indexInChunk, allocatedCount, outputEntities);

                outputEntities += allocatedCount;
                count -= allocatedCount;
            }
        }

        public unsafe void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            //@TODO: Not handling Entity existance... Stop mixing checks in the middle of code, seperate checks & runtime code

            //@TODO: Handle ISharedComponentData

            var componentType = ComponentType.Create<T>();
            Archetype* type = m_Entities.GetArchetype(entity);
            int t = 0;
            while (t < type->typesCount && type->types[t] < componentType)
            {
                m_CachedComponentTypeArray[t] = type->types[t];
                ++t;
            }
#if ENABLE_NATIVE_ARRAY_CHECKS
            if (t < type->typesCount && type->types[t] == componentType)
                throw new InvalidOperationException("Trying to add a component to an entity which is already present");
#endif
            m_CachedComponentTypeArray[t] = componentType;
            while (t < type->typesCount)
            {
                m_CachedComponentTypeArray[t + 1] = type->types[t];
                ++t;
            }
            Archetype* newType = m_ArchetypeManager.GetArchetype(m_CachedComponentTypeArray, type->typesCount + 1, m_GroupManager, m_SharedComponentManager);
            Chunk* newChunk = m_ArchetypeManager.GetChunkWithEmptySlots(newType);

            int newChunkIndex = ArchetypeManager.AllocateIntoChunk(newChunk);
            m_Entities.SetArchetype(m_ArchetypeManager, entity, newType, newChunk, newChunkIndex);
            SetComponent<T>(entity, componentData);
        }

        public unsafe void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
        {
            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            //@TODO: Not handling Entity existance... Stop mixing checks in the middle of code, seperate checks & runtime code

            ComponentType componentType = ComponentType.Create<T>();
            Archetype* type = m_Entities.GetArchetype(entity);
            int removedTypes = 0;
            for (int t = 0; t < type->typesCount; ++t)
            {
                if (type->types[t] == componentType)
                    ++removedTypes;
                else
                    m_CachedComponentTypeArray[t - removedTypes] = type->types[t];
            }
#if ENABLE_NATIVE_ARRAY_CHECKS
            if (removedTypes != 1)
                throw new InvalidOperationException("Trying to remove a component from an entity which is not present");
#endif

            Archetype* newType = m_ArchetypeManager.GetArchetype(m_CachedComponentTypeArray, type->typesCount - removedTypes, m_GroupManager, m_SharedComponentManager);

            Chunk* newChunk = m_ArchetypeManager.GetChunkWithEmptySlots(newType);
            int newChunkIndex = ArchetypeManager.AllocateIntoChunk(newChunk);
            m_Entities.SetArchetype(m_ArchetypeManager, entity, newType, newChunk, newChunkIndex);
        }

        public unsafe ComponentDataArrayFromEntity<T> GetComponentDataArrayFromEntity<T>() where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            return new ComponentDataArrayFromEntity<T>(m_JobSafetyManager.GetSafetyHandle(typeIndex), typeIndex, m_Entities);
        }


        public T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities.AssertEntityHasComponent(entity, typeIndex);
            m_JobSafetyManager.CompleteWriteDependency(typeIndex);

            IntPtr ptr = m_Entities.GetComponentDataWithType (entity, typeIndex);

            T value;
            UnsafeUtility.CopyPtrToStructure (ptr, out value);
            return value;
        }

        public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities.AssertEntityHasComponent(entity, typeIndex);

            m_JobSafetyManager.CompleteReadAndWriteDependency(typeIndex);

            IntPtr ptr = m_Entities.GetComponentDataWithType (entity, typeIndex);
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

        /// Shared component data
        //@TODO: Shared component data
        //@TODO: * Need to handle refcounting / destruction of archetypes, right now we just leak shared component types
        //@TODO: * Integrate into add component / remove component (Should be generalized to build on top of general purpose SetArchetype(Entity entity); API)
        public ComponentType CreateSharedComponentType<T>(T data) where T : struct, ISharedComponentData
        {
            return m_SharedComponentManager.InsertSharedComponent<T>(data);
        }

        public void GetAllUniqueSharedComponents(Type type, NativeList<ComponentType> types)
        {
            m_SharedComponentManager.GetAllUniqueSharedComponents(type, types);
        }

        public T GetSharedComponentData<T>(ComponentType componentType) where T : struct, ISharedComponentData
        {
            //@TODO: This really needs validation on if the compeont 
            return m_SharedComponentManager.GetSharedComponentData<T>(componentType);
        }

        unsafe public T GetSharedComponentData<T>(Entity entity) where T : struct, ISharedComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();

            m_Entities.AssertEntityHasComponent(entity, typeIndex);

            Archetype* archetype = m_Entities.GetArchetype(entity);
            int indexInTypeArray = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
            return m_SharedComponentManager.GetSharedComponentData<T>(archetype->types[indexInTypeArray].sharedComponentIndex);
        }

        public void CompleteAllJobs()
        {
            ComponentJobSafetyManager.CompleteAllJobsAndInvalidateArrays();
        }

        internal ComponentJobSafetyManager ComponentJobSafetyManager { get { return m_JobSafetyManager; } }
    }
}



