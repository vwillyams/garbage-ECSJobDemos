using System;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    [NativeContainer]
    public unsafe struct ComponentDataFromEntity<T> where T : struct, IComponentData
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle      m_Safety;
#endif
        EntityDataManager       m_Entities;
        int                     m_TypeIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal ComponentDataFromEntity(int typeIndex, EntityDataManager entityData, AtomicSafetyHandle safety)
        {
            m_Safety = safety;
            m_TypeIndex = typeIndex;
            m_Entities = entityData;
        }
#else
        internal ComponentDataArrayFromEntity(int typeIndex, EntityDataManager entityData)
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

        public unsafe T this[Entity entity]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                m_Entities.AssertEntityHasComponent(entity, m_TypeIndex);

                IntPtr ptr = m_Entities.GetComponentDataWithType(entity, m_TypeIndex);
                T data;
                UnsafeUtility.CopyPtrToStructure(ptr, out data);

                return data;
            }
			set
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                m_Entities.AssertEntityHasComponent(entity, m_TypeIndex);

                IntPtr ptr = m_Entities.GetComponentDataWithType(entity, m_TypeIndex);
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
			}
		}
	}
}