using System;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.ECS
{
	public struct ComponentArray<T> where T: Component
	{
	    private ComponentChunkIterator  m_Iterator;
	    private ComponentChunkCache 	m_Cache;
	    private readonly int                     m_Length;
	    private readonly ArchetypeManager		m_ArchetypeManager;

        internal ComponentArray(ComponentChunkIterator iterator, int length, ArchetypeManager typeMan)
		{
            m_Length = length;
            m_Cache = default(ComponentChunkCache);
			m_Iterator = iterator;
			m_ArchetypeManager = typeMan;
		}

		public T this[int index]
		{
			get
			{
                //@TODO: Unnecessary.. integrate into cache instead...
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
	                m_Iterator.UpdateCache(index, out m_Cache);

				return (T)m_Iterator.GetManagedObject(m_ArchetypeManager, m_Cache.CachedBeginIndex, index);
			}
		}

		public T[] ToArray()
		{
			var arr = new T[m_Length];
			var i = 0;
			while (i < m_Length)
			{
				m_Iterator.UpdateCache(i, out m_Cache);
				int start, length;
				var objs = m_Iterator.GetManagedObjectRange(m_ArchetypeManager, m_Cache.CachedBeginIndex, i, out start, out length);
				for (var obj = 0; obj < length; ++obj)
					arr[i+obj] = (T)objs[start+obj];
				i += length;
			}
			return arr;
		}

	    private void FailOutOfRangeError(int index)
		{
			throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
		}

		public int Length => m_Length;
	}
}
