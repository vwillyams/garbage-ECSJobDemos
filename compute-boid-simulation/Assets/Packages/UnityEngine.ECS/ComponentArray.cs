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
	public unsafe struct ComponentArray<T> where T: Component
	{
        ComponentDataArrayCache m_Cache;
        int                     m_Length;

		public unsafe ComponentArray(ComponentDataArchetypeSegment* data, int length)
		{
            m_Length = length;
            m_Cache = new ComponentDataArrayCache(data, length);
		}

		public unsafe T this[int index]
		{
			get
			{
                //@TODO: Unnecessary.. integrate into cache instead...
				if ((uint)index >= (uint)m_Length)
					FailOutOfRangeError(index);

                if (index < m_Cache.m_CachedBeginIndex || index >= m_Cache.m_CachedEndIndex)
                    m_Cache.UpdateCache(index);

                int instanceId = UnsafeUtility.ReadArrayElementWithStride<int>(m_Cache.m_CachedPtr, index, m_Cache.m_CachedStride);

                // Get component from entity
				return UnityEditor.EditorUtility.InstanceIDToObject (instanceId) as T;
			}
		}

		void FailOutOfRangeError(int index)
		{
			throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
		}

		public int Length { get { return m_Length; } }
	}
}