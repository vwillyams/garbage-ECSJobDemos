using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{
	public unsafe struct GameObjectArray
	{
        ComponentChunkIterator  m_Iterator;
		ComponentChunkCache 	m_Cache;
        int                     m_Length;
        ArchetypeManager		m_ArchetypeManager;

        internal unsafe GameObjectArray(ComponentChunkIterator iterator, int length, ArchetypeManager typeMan)
		{
            m_Length = length;
            m_Cache = default(ComponentChunkCache);
			m_Iterator = iterator;
			m_ArchetypeManager = typeMan;
		}

		public unsafe GameObject this[int index]
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
		    GameObject[] arr = new GameObject[m_Length];
			int i = 0;
			while (i < m_Length)
			{
				m_Iterator.UpdateCache(i, out m_Cache);
				int start, length;
				var objs = m_Iterator.GetManagedObjectRange(m_ArchetypeManager, m_Cache.CachedBeginIndex, i, out start, out length);
				for (int obj = 0; obj < length; ++obj)
					arr[i+obj] = ((Transform) objs[start + obj]).gameObject;
				i += length;
			}
			return arr;
		}

		void FailOutOfRangeError(int index)
		{
			throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
		}

		public int Length { get { return m_Length; } }
	}
}
