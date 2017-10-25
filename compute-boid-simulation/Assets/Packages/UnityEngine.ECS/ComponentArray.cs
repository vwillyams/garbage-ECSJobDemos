using UnityEngine;
using UnityEngine.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{
	public unsafe struct ComponentArray<T> where T: Component
	{
        ComponentDataArrayCache m_Cache;
        int                     m_Length;
        ArchetypeManager		m_ArchetypeManager;

        internal unsafe ComponentArray(ComponentDataArrayCache cache, int length, ArchetypeManager typeMan)
		{
            m_Length = length;
            m_Cache = cache;
			m_ArchetypeManager = typeMan;
		}

		public unsafe T this[int index]
		{
			get
			{
                //@TODO: Unnecessary.. integrate into cache instead...
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);

                if (index < m_Cache.CachedBeginIndex || index >= m_Cache.CachedEndIndex)
                    m_Cache.UpdateCache(index);

				return (T)m_Cache.GetManagedObject(m_ArchetypeManager, index);
			}
		}

		public T[] ToArray()
		{
			T[] arr = new T[m_Length];
			int i = 0;
			while (i < m_Length)
			{
				m_Cache.UpdateCache(i);
				int start, length;
				var objs = m_Cache.GetManagedObjectRange(m_ArchetypeManager, i, out start, out length);
				for (int obj = 0; obj < length; ++obj)
					arr[i+obj] = (T)objs[start+obj];
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