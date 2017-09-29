using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{
    public unsafe struct ComponentDataArrayFromEntity<T> where T : struct, IComponentData
    {
#if ENABLE_NATIVE_ARRAY_CHECKS
        AtomicSafetyHandle      m_Safety;
#endif
        EntityData*             m_Entities;
        int                     m_TypeIndex;

        internal ComponentDataArrayFromEntity(AtomicSafetyHandle safety, int typeIndex, EntityData* entityData)
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
            if (m_Entities[entity.index].version != entity.version)
                return false;

            return ChunkDataUtility.GetIndexInTypeArray(m_Entities[entity.index].archetype, m_TypeIndex) != -1;
        }

        public unsafe T this[Entity entity]
        {
            get
            {
#if ENABLE_NATIVE_ARRAY_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

                if (m_Entities[entity.index].version != entity.version)
                    throw new System.ArgumentException("Entity does not exist");
#endif

                IntPtr ptr = ChunkDataUtility.GetComponentDataWithType(m_Entities[entity.index].chunk, m_Entities[entity.index].index, m_TypeIndex);
                T data;
                UnsafeUtility.CopyPtrToStructure(ptr, out data);

                return data;
            }
			set
			{
#if ENABLE_NATIVE_ARRAY_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                if (m_Entities[entity.index].version != entity.version)
                    throw new System.ArgumentException("Entity does not exist");
#endif

                IntPtr ptr = ChunkDataUtility.GetComponentDataWithType(m_Entities[entity.index].chunk, m_Entities[entity.index].index, m_TypeIndex);
                UnsafeUtility.CopyStructureToPtr(ref value, ptr);
			}
		}
	}
}