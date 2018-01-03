using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace UnityEngine.ECS
{
    [NativeContainer]
    unsafe public struct EntityTransaction
    {
        AtomicSafetyHandle                 m_Safety;

        [NativeDisableUnsafePtrRestriction]
        GCHandle                           m_ArchetypeManager;

        [NativeDisableUnsafePtrRestriction]
        EntityDataManager*                 m_Entities;

        [NativeDisableUnsafePtrRestriction]
        ComponentTypeInArchetype*          m_CachedComponentTypeInArchetypeArray;

        internal EntityTransaction(ArchetypeManager archetypes, EntityDataManager* data)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = new AtomicSafetyHandle();
#endif
            m_Entities = data;
            m_ArchetypeManager = GCHandle.Alloc(archetypes, GCHandleType.Weak);
            m_CachedComponentTypeInArchetypeArray = (ComponentTypeInArchetype*)UnsafeUtility.Malloc(sizeof(ComponentTypeInArchetype) * 32 * 1024, 16, Allocator.Persistent);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal void SetAtomicSafetyHandle(AtomicSafetyHandle safety)
        {
            m_Safety = safety;
        }
#endif

        unsafe int PopulatedCachedTypeInArchetypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeInArchetypeArray[0] = new ComponentTypeInArchetype(ComponentType.Create<Entity>());
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeInArchetypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void CheckAccess()
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            #endif
        }

        unsafe public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            CheckAccess();

            var archetypeManager = (ArchetypeManager)m_ArchetypeManager.Target;

            EntityArchetype type;
            //@TODO: make dedicated function to only allow getting existing archetype
            type.archetype = archetypeManager.GetArchetype(m_CachedComponentTypeInArchetypeArray, PopulatedCachedTypeInArchetypeArray(types), null, null);
            return type;
        }

        unsafe public Entity CreateEntity(EntityArchetype archetype)
        {
            CheckAccess();

            Entity entity;
            CreateEntityInternal(archetype, &entity, 1);
            return entity;
        }

        unsafe public void CreateEntity(EntityArchetype archetype, NativeArray<Entity> entities)
        {
            CreateEntityInternal(archetype, (Entity*)entities.GetUnsafePtr(), entities.Length);
        }

        unsafe public Entity CreateEntity(params ComponentType[] types)
        {
            return CreateEntity(CreateArchetype(types));
        }

        unsafe void CreateEntityInternal(EntityArchetype archetype, Entity* entities, int count)
        {
            CheckAccess();

            var archetypeManager = (ArchetypeManager)m_ArchetypeManager.Target;

            while (count != 0)
            {
                Chunk* chunk = archetypeManager.GetChunkWithEmptySlots(archetype.archetype);
                int allocatedIndex;
                int allocatedCount = archetypeManager.AllocateIntoChunk(chunk, count, out allocatedIndex);
                m_Entities->AllocateEntities(archetype.archetype, chunk, allocatedIndex, allocatedCount, entities);
                ChunkDataUtility.ClearComponents(chunk, allocatedIndex, allocatedCount);

                entities += allocatedCount;
                count -= allocatedCount;
            }
        }

        public bool Exists(Entity entity)
        {
            CheckAccess();

            return m_Entities->ExistsFromTransaction(entity);
        }

        public T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            CheckAccess();

            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponentFromTransaction(entity, typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);

            T data;
            UnsafeUtility.CopyPtrToStructure(ptr, out data);
            return data;
        }

        unsafe public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            CheckAccess();

            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponentFromTransaction(entity, typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);
            UnsafeUtility.CopyStructureToPtr (ref componentData, ptr);
        }

        internal void Dispose()
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            //@TODO: Check if this is sensible...
            AtomicSafetyHandle.EnforceAllBufferJobsHaveCompletedAndRelease(m_Safety);
            #endif

            m_ArchetypeManager.Free();
            m_Entities = null;
        }

        //@TODO: SharedComponentData API, Fixed Array API
    }
}
