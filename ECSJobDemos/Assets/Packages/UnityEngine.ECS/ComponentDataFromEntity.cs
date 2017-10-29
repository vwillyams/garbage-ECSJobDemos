using System;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    public unsafe struct ComponentDataArrayFromEntity<T> where T : struct, IComponentData
    {
#if ENABLE_NATIVE_ARRAY_CHECKS
        AtomicSafetyHandle      m_Safety;
#endif
        EntityDataManager       m_Entities;
        int                     m_TypeIndex;

        internal ComponentDataArrayFromEntity(AtomicSafetyHandle safety, int typeIndex, EntityDataManager entityData)
        {
#if ENABLE_NATIVE_ARRAY_CHECKS
            m_Safety = safety;
#endif
            m_TypeIndex = typeIndex;
            m_Entities = entityData;
        }

        public bool Exists(Entity entity)
        {
#if ENABLE_NATIVE_ARRAY_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            //@TODO: out of bounds index checks...

            return m_Entities.HasComponent(entity, m_TypeIndex);
        }

        public unsafe T this[Entity entity]
        {
            get
            {
#if ENABLE_NATIVE_ARRAY_CHECKS
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
#if ENABLE_NATIVE_ARRAY_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                m_Entities.AssertEntityHasComponent(entity, m_TypeIndex);

                IntPtr ptr = m_Entities.GetComponentDataWithType(entity, m_TypeIndex);
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
			}
		}
	}
}