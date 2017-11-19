using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace UnityEngine.ECS
{
    [NativeContainer]
    public struct FixedArrayFromEntity<T> where T : struct
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle      m_Safety;
#endif
        EntityDataManager       m_Entities;
        int                     m_TypeIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal FixedArrayFromEntity(int typeIndex, EntityDataManager entityData, AtomicSafetyHandle safety)
        {
            m_Safety = safety;
            m_TypeIndex = typeIndex;
            m_Entities = entityData;
        }
#else
        internal FixedArrayFromEntity(int typeIndex, EntityDataManager entityData)
        {
            m_TypeIndex = typeIndex;
            m_Entities = entityData;
        }
#endif

        public bool Exists(Entity entity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            //@TODO: out of bounds index checks...

            return m_Entities.HasComponent(entity, m_TypeIndex);
        }
        
        public unsafe NativeArray<T> this[Entity entity]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Note that this check is only for the lookup table into the entity manager
                // The native array performs the actual read only / write only checks
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

                m_Entities.AssertEntityHasComponent(entity, m_TypeIndex);
                if (TypeManager.GetComponentType(m_TypeIndex).category != TypeManager.TypeCategory.OtherValueType)
                    throw new ArgumentException($"GetComponentFixedArray<{typeof(T)}> may not be IComponentData or ISharedComponentData");
#endif
        
                IntPtr ptr;
                int length;
                m_Entities.GetComponentDataWithTypeAndFixedArrayLength (entity, m_TypeIndex, out ptr, out length);

                var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, length, Allocator.Invalid);
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety); 
#endif

                return array;
            }
        }
    }
}