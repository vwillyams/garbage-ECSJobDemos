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

        public SharedComponentDataManager()
        {
            m_SharedComponentData.Add(null);
            m_SharedComponentRefCount.Add(1);
        }

        public void Dispose()
        {
            m_SharedComponentRefCount.Dispose();
            m_SharedComponentData.Clear();
            m_SharedComponentData = null;
        }

        public void GetAllUniqueSharedComponents(System.Type componentType, NativeList<int> componentIndices)
        {
            int typeIndex = TypeManager.GetTypeIndex(componentType);
            for (int i = 1; i != m_SharedComponentData.Count; i++)
            {
                object data = m_SharedComponentData[i];
                if (data != null && data.GetType() == componentType)
                    componentIndices.Add(i);
            }
        }

        public int InsertSharedComponent<T>(T newData) where T : struct
        {
            if (newData.Equals(default(T)))
            {
                return 0;
            }

            for (int i = 1; i != m_SharedComponentData.Count; i++)
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

        public T GetSharedComponentData<T>(int index) where T : struct
        {
            if (index == 0)
            {
                return default(T);
            }
            else
            {
                return (T)m_SharedComponentData[index];
            }
        }

        public void AddReference(int index)
        {
            if (index != 0)
                ++m_SharedComponentRefCount[index];
        }

        public void RemoveReference(int index)
        {
            if (index == 0)
                return;
            int newCount = --m_SharedComponentRefCount[index];
            if (newCount == 0)
            {
                m_SharedComponentData[index] = null;
            }
        }
    }
}
