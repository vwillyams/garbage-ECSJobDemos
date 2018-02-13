using System;
using Unity.ECS;

namespace UnityEngine.ECS
{
	public struct GameObjectArray
	{
	    private ComponentChunkIterator  m_Iterator;
	    private ComponentChunkCache 	m_Cache;
	    private readonly int                     m_Length;
	    private readonly ArchetypeManager		m_ArchetypeManager;

        internal GameObjectArray(ComponentChunkIterator iterator, int length, ArchetypeManager typeMan)
		{
            m_Length = length;
            m_Cache = default(ComponentChunkCache);
			m_Iterator = iterator;
			m_ArchetypeManager = typeMan;
		}

		public GameObject this[int index]
		{
			get
			{
                //@TODO: Unnecessary.. integrate into cache instead...
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
	                m_Iterator.UpdateCache(index, out m_Cache);

			    var transform = (Transform) m_Iterator.GetManagedObject(m_ArchetypeManager, m_Cache.CachedBeginIndex, index);

				return transform.gameObject;
			}
		}

		public GameObject[] ToArray()
		{
		    var arr = new GameObject[m_Length];
			var i = 0;
			while (i < m_Length)
			{
				m_Iterator.UpdateCache(i, out m_Cache);
				int start, length;
				var objs = m_Iterator.GetManagedObjectRange(m_ArchetypeManager, m_Cache.CachedBeginIndex, i, out start, out length);
				for (var obj = 0; obj < length; ++obj)
					arr[i+obj] = ((Transform) objs[start + obj]).gameObject;
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
