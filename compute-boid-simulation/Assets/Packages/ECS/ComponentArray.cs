using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;
#pragma warning disable 0649

namespace ECS
{
    public struct ComponentArray<T> where T : Component
    {
        public List<T> m_List;

		public int Length { get { return m_List.Count; } }

        public unsafe T this[int index]
        {
            get
            {
				return m_List[index];
            }
        }
    }
}
