using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace UnityEngine.ECS
{
    //@TODO: NOTE THIS CODE HAS NO PROTECTION AGAINST ACTUALLY PREVENTING RACE CONDITIONS!!!
    
    [NativeContainer]
    unsafe public struct EntityTransaction
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle                 m_Safety;
#endif
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
            type.archetype = archetypeManager.GetExistingArchetype(m_CachedComponentTypeInArchetypeArray, PopulatedCachedTypeInArchetypeArray(types));
            
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (type.archetype == null)
                throw new System.ArgumentException("EntityTransaction.CreateArchetype may only lookup existing archetypes");
            #endif
            
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
            m_Entities->CreateEntities(archetypeManager, archetype.archetype, entities, count, false);
        }

        public bool Exists(Entity entity)
        {
            CheckAccess();

            return m_Entities->ExistsFromTransaction(entity);
        }

        public T GetComponentData<T>(Entity entity) where T : struct, IComponentData
        {
            CheckAccess();

            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponentFromTransaction(entity, typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);

            T data;
            UnsafeUtility.CopyPtrToStructure(ptr, out data);
            return data;
        }

        unsafe public void SetComponentData<T>(Entity entity, T componentData) where T: struct, IComponentData
        {
            CheckAccess();

            int typeIndex = TypeManager.GetTypeIndex<T>();
            m_Entities->AssertEntityHasComponentFromTransaction(entity, typeIndex);

            byte* ptr = m_Entities->GetComponentDataWithType (entity, typeIndex);
            UnsafeUtility.CopyStructureToPtr (ref componentData, ptr);
        }

        internal void OnDestroyManager()
        {
            UnsafeUtility.Free(m_CachedComponentTypeInArchetypeArray, Allocator.Persistent);
            m_ArchetypeManager.Free();
            m_Entities = null;
        }

        //@TODO: SharedComponentData API, Fixed Array API
    }
}
