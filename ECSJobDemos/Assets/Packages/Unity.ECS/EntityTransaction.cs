using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace UnityEngine.ECS
{
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
            m_Safety = new AtomicSafetyHandle();
            m_Entities = data;
            m_ArchetypeManager = GCHandle.Alloc(archetypes);
            m_CachedComponentTypeInArchetypeArray = (ComponentTypeInArchetype*)UnsafeUtility.Malloc(sizeof(ComponentTypeInArchetype) * 32 * 1024, 16, Allocator.Persistent);
        }

        unsafe int PopulatedCachedTypeInArchetypeArray(ComponentType[] requiredComponents)
        {
            m_CachedComponentTypeInArchetypeArray[0] = new ComponentTypeInArchetype(ComponentType.Create<Entity>());
            for (int i = 0; i < requiredComponents.Length; ++i)
                SortingUtilities.InsertSorted(m_CachedComponentTypeInArchetypeArray, i + 1, requiredComponents[i]);
            return requiredComponents.Length + 1;
        }

        unsafe public EntityArchetype CreateArchetype(params ComponentType[] types)
        {
            var archetypeManager = (ArchetypeManager)m_ArchetypeManager.Target;

            EntityArchetype type;
            //@TODO: make dedicated function to only allow getting existing archetype
            type.archetype = archetypeManager.GetArchetype(m_CachedComponentTypeInArchetypeArray, PopulatedCachedTypeInArchetypeArray(types), null, null);
            return type;
        }


        unsafe public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponent(entity, typeIndex);


            //@TODO: Prevent access to already created entities
            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);
            UnsafeUtility.CopyStructureToPtr (ref componentData, ptr);
        }

        unsafe public Entity CreateEntity(EntityArchetype archetype)
        {
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

        internal void Dispose()
        {
            m_ArchetypeManager.Free();
            m_Entities = null;
            //@TODO:
            //m_ArchetypeManager.IntegrateChunks();
        }
    }
}
