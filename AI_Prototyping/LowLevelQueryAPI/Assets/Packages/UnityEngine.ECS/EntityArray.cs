using UnityEngine;
using UnityEngine.Collections;

namespace UnityEngine.ECS
{
	public struct EntityArray
	{
		internal NativeArray<Entity> m_Array;

		public int Length { get { return m_Array.Length; } }

		public unsafe Entity this[int index]
		{
			get
			{
				return m_Array[index];
			}
		}
	}
}