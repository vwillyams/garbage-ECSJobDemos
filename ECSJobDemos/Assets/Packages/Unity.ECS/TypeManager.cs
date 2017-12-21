using System;
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

        public enum TypeCategory
        {
            IComponentData,
            ISharedComponentData,
            OtherValueType,
            EntityData,
            Class
        }

        public struct ComponentType
        {
            public ComponentType(Type type, int size, TypeCategory category)
            {
                this.type = type;
                this.sizeInChunk = size;
                this.category = category;
            }

            public Type          type;
            public int           sizeInChunk;
            public TypeCategory  category;
        }

        static List<ComponentType> m_Types;

        public static void Initialize()
        {
            if (m_Types == null)
            {
                m_Types = new List<ComponentType> ();
                m_Types.Add (new ComponentType(null, 0, TypeCategory.IComponentData));
                // This must always be first so that Entity is always index 0 in the archetype
                m_Types.Add(new ComponentType(typeof(Entity), sizeof(Entity), TypeCategory.EntityData));
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
                if (c.type == type)
                {
                    typeIndex = i;
                    break;
                }
            }

            if (typeIndex != -1)
                return typeIndex;
            else
            {
                int componentSize = 0;
                TypeCategory category;

                if (typeof(IComponentData).IsAssignableFrom(type))
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (type.IsClass)
                        throw new System.ArgumentException($"{type} is an IComponentData, and thus must be a struct.");
                    if (!UnsafeUtility.IsBlittable(type))
                        throw new System.ArgumentException($"{type} is an IComponentData, and thus must be blittable (No managed object is allowed on the struct).");
#endif

                    category = TypeCategory.IComponentData;
                    componentSize = UnsafeUtility.SizeOf(type);
                }
                else if (typeof(ISharedComponentData).IsAssignableFrom(type))
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (type.IsClass)
                        throw new System.ArgumentException($"{type} is an ISharedComponentData, and thus must be a struct.");
#endif

                    category = TypeCategory.ISharedComponentData;
                }
                else if (type.IsValueType)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (!UnsafeUtility.IsBlittable(type))
                        throw new System.ArgumentException($"{type} is used for FixedArrays, and thus must be blittable.");
#endif
                    category = TypeCategory.OtherValueType;
                    componentSize = UnsafeUtility.SizeOf(type);
                }
                else if (type.IsClass)
                {
                    category = TypeCategory.Class;
                }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                else
                {
                    throw new System.ArgumentException($"'{type}' is not a valid component");
                }
#else
                else
                {
                    category = TypeCategory.OtherValueType;
                }
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (typeof(IComponentData).IsAssignableFrom(type) && typeof(ISharedComponentData).IsAssignableFrom(type))
                    throw new System.ArgumentException($"Component {type} can not be both IComponentData & ISharedComponentData");
#endif
                m_Types.Add (new ComponentType(type, componentSize, category));
                return m_Types.Count - 1;
            }
        }

        public static bool IsValidComponentTypeForArchetype(int typeIndex, bool isArray)
        {
            if (m_Types[typeIndex].category == TypeCategory.OtherValueType)
                return isArray;
            else
                return !isArray;
        }

        static public ComponentType GetComponentType(int typeIndex)
        {
            return m_Types[typeIndex];
        }

        static public ComponentType GetComponentType<T>()
        {
            return m_Types[GetTypeIndex<T>()];
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

