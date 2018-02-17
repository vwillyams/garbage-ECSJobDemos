using System.Collections.Generic;
using Unity.Collections;

namespace Unity.ECS
{
    class SharedComponentDataManager
    {
        NativeMultiHashMap<int, int> m_HashLookup = new NativeMultiHashMap<int, int>(128, Allocator.Persistent);

        List<object>    m_SharedComponentData = new List<object>();
        NativeList<int> m_SharedComponentRefCount = new NativeList<int>(0, Allocator.Persistent);
        NativeList<int> m_SharedComponentVersion = new NativeList<int>(0, Allocator.Persistent);
        NativeList<int> m_SharedComponentType = new NativeList<int>(0, Allocator.Persistent);

        public SharedComponentDataManager()
        {
            m_SharedComponentData.Add(null);
            m_SharedComponentRefCount.Add(1);
            m_SharedComponentVersion.Add(1);
            m_SharedComponentType.Add(-1);
        }

        public void Dispose()
        {
            m_SharedComponentType.Dispose();
            m_SharedComponentRefCount.Dispose();
            m_SharedComponentVersion.Dispose();
            m_SharedComponentData.Clear();
            m_SharedComponentData = null;
            m_HashLookup.Dispose();
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
            if (newData.Equals(default(T)))
                return 0;
            
            var typeIndex = TypeManager.GetTypeIndex<T>();
            
            return InsertSharedComponentAssumeNonDefault(typeIndex, newData.GetHashCode(), newData);
        }
        
        private int FindSharedComponentIndex<T>(int typeIndex, T newData) where T : struct
        {
            if (newData.Equals(default(T)))
                return 0;
            else
                return FindNonDefaultSharedComponentIndex(typeIndex, newData.GetHashCode(), newData);
        }
      
        private int FindNonDefaultSharedComponentIndex(int typeIndex, int hashCode, object newData)
        {
            int itemIndex;
            NativeMultiHashMapIterator<int> iter;

            if (!m_HashLookup.TryGetFirstValue(hashCode, out itemIndex, out iter))
                return -1;

            do
            {
                var data = m_SharedComponentData[itemIndex];
                if (data != null && m_SharedComponentType[itemIndex] == typeIndex)
                {
                    if (newData.Equals(data))
                    {
                        return itemIndex;
                    }
                }
            }
            while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));

            return -1;
        }
        
        
        int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData)
        {
            int index = FindNonDefaultSharedComponentIndex(typeIndex, hashCode, newData);

            if (-1 == index)
            {
                index = m_SharedComponentData.Count;
                m_HashLookup.Add(hashCode, index);
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
            var hashCode = m_SharedComponentData[index].GetHashCode();
            
            m_SharedComponentData[index] = null;
            m_SharedComponentType[index] = -1;

            int itemIndex;
            NativeMultiHashMapIterator<int> iter;
            if (!m_HashLookup.TryGetFirstValue(hashCode, out itemIndex, out iter))
                return;

            do
            {
                if (itemIndex != index)
                    continue;

                m_HashLookup.Remove(iter);
                break;
            }
            while (m_HashLookup.TryGetNextValue(out itemIndex, ref iter));
        }

        
        unsafe public void MoveSharedComponents(SharedComponentDataManager srcSharedComponents, int* sharedComponentIndices, int sharedComponentIndicesCount)
        {
            for (int i = 0;i != sharedComponentIndicesCount;i++)
            {
                int srcIndex = sharedComponentIndices[i];
                if (srcIndex == 0)
                    continue;

                var srcData = srcSharedComponents.m_SharedComponentData[srcIndex];
                int dstIndex = InsertSharedComponentAssumeNonDefault(srcSharedComponents.m_SharedComponentType[srcIndex], srcData.GetHashCode(), srcData);
                srcSharedComponents.RemoveReference(srcIndex);

                sharedComponentIndices[i] = dstIndex;
            }
        }
    }
}
