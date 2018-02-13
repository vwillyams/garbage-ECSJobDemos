using System.Collections.Generic;
using Unity.Collections;

namespace Unity.ECS
{
    class SharedComponentDataManager
    {
        NativeMultiHashMap<int, int> m_TypeLookup;

        int             m_RefCount;
        List<object>    m_SharedComponentData = new List<object>();
        NativeList<int> m_SharedComponentRefCount = new NativeList<int>(0, Allocator.Persistent);
        NativeList<int> m_SharedComponentVersion = new NativeList<int>(0, Allocator.Persistent);
        NativeList<int> m_SharedComponentType = new NativeList<int>(0, Allocator.Persistent);

        public SharedComponentDataManager()
        {
            m_TypeLookup = new NativeMultiHashMap<int, int>(128, Allocator.Persistent);
            m_SharedComponentData.Add(null);
            m_SharedComponentRefCount.Add(1);
            m_SharedComponentVersion.Add(1);
            m_SharedComponentType.Add(-1);
        }

        public void Retain()
        {
            m_RefCount++;
        }

        public bool Release()
        {
            if (--m_RefCount != 0)
                return false;

            m_SharedComponentType.Dispose();
            m_SharedComponentRefCount.Dispose();
            m_SharedComponentVersion.Dispose();
            m_SharedComponentData.Clear();
            m_SharedComponentData = null;
            m_TypeLookup.Dispose();
            return true;

        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues) where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            for (var i = 1; i != m_SharedComponentData.Count; i++)
            {
                var data = m_SharedComponentData[i];
                if (data != null && data.GetType() == typeof(T))
                    sharedComponentValues.Add((T)m_SharedComponentData[i]);
            }
        }

        public int InsertSharedComponent<T>(T newData) where T : struct
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var index = FindSharedComponentIndex(typeIndex, newData);

            if (-1 == index)
            {
                index = m_SharedComponentData.Count;
                m_TypeLookup.Add(typeIndex, index);
                m_SharedComponentData.Add(newData);
                m_SharedComponentRefCount.Add(1);
                m_SharedComponentVersion.Add(1);
                m_SharedComponentType.Add(typeIndex);
            }
            else
            {
                m_SharedComponentRefCount[index] += 1;
            }

            return index;
        }

        int FindSharedComponentIndex<T>(int typeIndex, T newData) where T : struct
        {
            int itemIndex;
            NativeMultiHashMapIterator<int> iter;

            if (newData.Equals(default(T)))
            {
                return 0;
            }

            if (!m_TypeLookup.TryGetFirstValue(typeIndex, out itemIndex, out iter))
                return -1;

            do
            {
                var data = m_SharedComponentData[itemIndex];
                if (data != null)
                {
                    if (newData.Equals(data))
                    {
                        return itemIndex;
                    }
                }
            }
            while (m_TypeLookup.TryGetNextValue(out itemIndex, ref iter));

            return -1;
        }

        public void IncrementSharedComponentVersion(int index)
        {
            m_SharedComponentVersion[index]++;
        }

        public int GetSharedComponentVersion<T>(T sharedData) where T: struct
        {
            var index = FindSharedComponentIndex(TypeManager.GetTypeIndex<T>(), sharedData);
            return index == -1 ? 0 : m_SharedComponentVersion[index];
        }

        public T GetSharedComponentData<T>(int index) where T : struct
        {
            if (index == 0)
                return default(T);

            return (T)m_SharedComponentData[index];
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

            var newCount = --m_SharedComponentRefCount[index];

            if (newCount != 0)
                return;

            var typeIndex = m_SharedComponentType[index];

            m_SharedComponentData[index] = null;
            m_SharedComponentType[index] = -1;

            int itemIndex;
            NativeMultiHashMapIterator<int> iter;
            if (!m_TypeLookup.TryGetFirstValue(typeIndex, out itemIndex, out iter))
                return;

            do
            {
                if (itemIndex != index)
                    continue;

                m_TypeLookup.Remove(iter);
                break;
            }
            while (m_TypeLookup.TryGetNextValue(out itemIndex, ref iter));
        }
    }
}
