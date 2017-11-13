﻿using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    unsafe static class TypeManager
    {
        struct StaticTypeLookup<T>
        {
            public static int typeIndex;
        }


        public struct ComponentType
        {
            public ComponentType(Type type, int size, bool requireManagedClass, int arrayElements=0)
            {
                this.type = type;
                this.sizeInChunk = size;
                this.arrayElements = arrayElements;
                this.requireManagedClass = requireManagedClass;
            }

            public Type     type;
            public int      sizeInChunk;
            public int      arrayElements;
            public bool     requireManagedClass;
        }

        static List<ComponentType> m_Types;

        public static void Initialize()
        {
            if (m_Types == null)
            {
                m_Types = new List<ComponentType> ();
                m_Types.Add (new ComponentType(null, 0, false));
                // This must always be first so that Entity is always index 0 in the archetype
                m_Types.Add(new ComponentType(typeof(Entity), sizeof(Entity), false));
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
            int typeIndex = -1;
            for (int i = 0; i != m_Types.Count;i++)
            {
                var c = m_Types[i];
                if (c.arrayElements == 0 && c.type == type)
                {
                    typeIndex = i;
                    break;
                }
            }

            if (typeIndex != -1)
                return typeIndex;
            else
            {
                int componentSize = 4;

                if (typeof(IComponentData).IsAssignableFrom(type))
                {
                    if (type.IsClass)
                        throw new System.ArgumentException(string.Format("{0} is an IComponentData, and thus must be a struct.", type));
                    if (!UnsafeUtility.IsBlittable(type))
                        throw new System.ArgumentException(string.Format("{0} is an IComponentData, and thus must be blittable (No managed object is allowed on the struct).", type));
                }

                if (typeof(IComponentData).IsAssignableFrom(type))
                {
                    componentSize = UnsafeUtility.SizeOf(type);
                }

                if (typeof(IComponentData).IsAssignableFrom(type) && typeof(ISharedComponentData).IsAssignableFrom(type))
                    throw new System.ArgumentException("A component can not be both IComponentData & ISharedComponentData");

                m_Types.Add (new ComponentType(type, componentSize, type.IsClass));
                return m_Types.Count - 1;
            }
        }

        public static int CreateArrayType(Type type, int numElements)
        {
            if (type.IsClass)
                throw new System.ArgumentException("Array type must be a blittable struct");

            m_Types.Add(new ComponentType(type, UnsafeUtility.SizeOf(type) * numElements, false, numElements));
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

