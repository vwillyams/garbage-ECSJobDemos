using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace UnityEngine.ECS
{
    class SharedComponentDataManager
    {
        List<object>    m_SharedComponentData = new List<object>();
        NativeList<int> m_SharedComponentRefCount = new NativeList<int>(0, Allocator.Persistent);

        public void Dispose()
        {
            m_SharedComponentRefCount.Dispose();
            m_SharedComponentData.Clear();
            m_SharedComponentData = null;
        }

// TODO: how to handle this now
//        public void GetAllUniqueSharedComponents(System.Type componentType, NativeList<ComponentType> uniqueComponents)
//        {
//            int typeIndex = TypeManager.GetTypeIndex(componentType);
//            for (int i = 0; i != m_SharedComponentData.Count; i++)
//            {
//                object data = m_SharedComponentData[i];
//                if (data != null && data.GetType() == componentType)
//                    uniqueComponents.Add(GetComponentType(typeIndex, i));
//            }
//        }

        unsafe public void OnArchetypeAdded(ComponentTypeInArchetype* types, int count)
        {
            //TODO: still need this?
        }

        public int InsertSharedComponent<T>(T newData) where T : struct
        {
            for (int i = 0; i != m_SharedComponentData.Count; i++)
            {
                object data = m_SharedComponentData[i];
                if (data != null && data.GetType() == typeof(T))
                {
                    if (newData.Equals(data))
                    {
                        m_SharedComponentRefCount[i]++;
                        return i;
                    }
                }
            }

            m_SharedComponentData.Add(newData);
            m_SharedComponentRefCount.Add(1);

            return m_SharedComponentData.Count - 1;
        }

        public T GetSharedComponentData<T>(int index)
        {
            return (T)m_SharedComponentData[index];
        }

        public void AdjustInstanceCount(int index, int count)
        {
            int newCount = m_SharedComponentRefCount[index];
            newCount += count;
            if (newCount == 0)
            {
                m_SharedComponentData[index] = null;
            }
        }
    }
}
