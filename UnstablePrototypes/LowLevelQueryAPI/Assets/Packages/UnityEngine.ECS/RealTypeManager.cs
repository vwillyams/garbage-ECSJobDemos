using System;
using System.Collections.Generic;

namespace UnityEngine.ECS
{
    unsafe public struct RealTypeManager
    {
        struct StaticTypeLookup<T>
        {
            public static int typeIndex;
        }


        public struct ComponentType
        {
            public ComponentType(Type type, int size, int arrayElements=0)
            {
                this.type = type;
                this.size = size;
                this.arrayElements = arrayElements;
            }
            public Type type;
            public int size;
            public int arrayElements;
        }

        static List<ComponentType> m_Types;

        public static void Initialize()
        {
            if (m_Types == null)
            {
                m_Types = new List<ComponentType> ();
                m_Types.Add (new ComponentType(null, 0));
                // This must always be first so that Entity is always index 0 in the archetype
                m_Types.Add(new ComponentType(typeof(Entity), sizeof(Entity)));
            }
        }

        public static int GetTypeIndex<T>()
        {
            int typeIndex = StaticTypeLookup<T>.typeIndex;
            if (typeIndex != 0)
            {
                return typeIndex;
            }
            else
            {
                typeIndex = GetTypeIndex (typeof(T));
                StaticTypeLookup<T>.typeIndex = typeIndex;
                return typeIndex;
            }
        }
           
        public static int GetTypeIndex(Type type)
        {
            int typeIndex = m_Types.FindIndex(c => (c.arrayElements == 0 && c.type == type));
            if (typeIndex != -1)
                return typeIndex;
            else
            {
                int componentSize = 4;
                if (!type.IsClass)
                {
                    componentSize = UnsafeUtility.SizeOfStruct(type);
                }
                m_Types.Add (new ComponentType(type, componentSize));
                return m_Types.Count - 1;
            }
        }

        public static int CreateArrayType(Type type, int numElements)
        {
            m_Types.Add(new ComponentType(type, UnsafeUtility.SizeOfStruct(type) * numElements, numElements));
            return m_Types.Count - 1;
        }

        static public ComponentType GetComponentType(int typeIndex)
        {
            return m_Types[typeIndex];
        }

        public static Type GetType(int typeIndex)
        {
            return m_Types[typeIndex].type;
        }

        public static int GetTypeCount()
        {
            return m_Types.Count;
        }
    }
}

