using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.ECS;
using UnityEngine;

namespace Unity.Core.Hybrid
{
    public static class EntityManagerExtensions
    {
        public static Entity Instantiate(this EntityManager entityManager, GameObject srcGameObject)
        {
            var components = srcGameObject.GetComponents<ComponentDataWrapperBase>();
            var componentTypes = new ComponentType[components.Length];
            for (var t = 0; t != components.Length; ++t)
                componentTypes[t] = components[t].GetComponentType(entityManager);

            var srcEntity = entityManager.CreateEntity(componentTypes);
            for (var t = 0; t != components.Length; ++t)
                components[t].UpdateComponentData(entityManager, srcEntity);

            return srcEntity;
        }

        public static unsafe void Instantiate(this EntityManager entityManager, GameObject srcGameObject, NativeArray<Entity> outputEntities)
        {
            if (outputEntities.Length == 0)
                return;

            var entity = entityManager.Instantiate(srcGameObject);
            outputEntities[0] = entity;

            var entityPtr = (Entity*)outputEntities.GetUnsafePtr();
            entityManager.InstantiateInternal(entity, entityPtr + 1, outputEntities.Length - 1);
        }

        public static unsafe T GetComponentObject<T>(this EntityManager entityManager, Entity entity) where T : Component
        {
            var componentType = ComponentType.Create<T>();
            entityManager.Entities->AssertEntityHasComponent(entity, componentType.TypeIndex);

            Chunk* chunk;
            int chunkIndex;
            entityManager.Entities->GetComponentChunk(entity, out chunk, out chunkIndex);
            return entityManager.ArchetypeManager.GetManagedObject(chunk, componentType, chunkIndex) as T;
        }
    }
}
