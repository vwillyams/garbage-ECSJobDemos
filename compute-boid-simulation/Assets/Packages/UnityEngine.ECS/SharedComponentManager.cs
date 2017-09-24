using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;

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

        public void GetAllUniqueSharedComponents(System.Type componentType, NativeList<ComponentType> uniqueComponents)
        {
            int typeIndex = RealTypeManager.GetTypeIndex(componentType);
            for (int i = 0; i != m_SharedComponentData.Count; i++)
            {
                object data = m_SharedComponentData[i];
                if (data != null && data.GetType() == componentType)
                    uniqueComponents.Add(GetComponentType(typeIndex, i));
            }
        }

        public ComponentType InsertSharedComponent<T>(T newData) where T : struct
        {
            for (int i = 0; i != m_SharedComponentData.Count; i++)
            {
                object data = m_SharedComponentData[i];
                if (data != null && data.GetType() == typeof(T))
                {
                    if (newData.Equals(data))
                    {
                        m_SharedComponentRefCount[i]++;
                        return GetComponentType(RealTypeManager.GetTypeIndex<T>(), i);
                    }
                }
            }

            m_SharedComponentData.Add(newData);
            m_SharedComponentRefCount.Add(1);

            return GetComponentType(RealTypeManager.GetTypeIndex<T>(), m_SharedComponentData.Count - 1);
        }

        public T GetSharedComponentData<T>(int index)
        {
            return (T)m_SharedComponentData[index];
        }

        public T GetSharedComponentData<T>(ComponentType component)
        {
            return (T)m_SharedComponentData[component.sharedComponentIndex];
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

        ComponentType GetComponentType(int typeIndex, int sharedIndex)
        {
            ComponentType type;
            type.sharedComponentIndex = sharedIndex;
            type.typeIndex = typeIndex;
            return type;
        }
    }
}
