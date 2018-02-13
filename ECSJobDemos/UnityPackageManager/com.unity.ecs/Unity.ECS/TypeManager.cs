using System;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.ECS;

namespace Unity.ECS
{
    internal static unsafe class TypeManager
    {
        private struct StaticTypeLookup<T>
        {
            public static int typeIndex;
        }

        internal enum TypeCategory
        {
            IComponentData,
            ISharedComponentData,
            OtherValueType,
            EntityData,
            Class
        }

        internal struct ComponentType
        {
            public ComponentType(Type type, int size, TypeCategory category)
            {
                Type = type;
                SizeInChunk = size;
                Category = category;
            }

            public readonly Type          Type;
            public readonly int           SizeInChunk;
            public readonly TypeCategory  Category;
        }

        private static ComponentType[]    m_Types;
        private static volatile int       m_Count;
        private static SpinLock           m_CreateTypeLock;

        public const int MaximumTypesCount = 1024 * 10;

        public static void Initialize()
        {
            if (m_Types != null)
                return;

            m_CreateTypeLock = new SpinLock();
            m_Types = new ComponentType[MaximumTypesCount];
            m_Count = 0;

            m_Types[m_Count++] = new ComponentType(null, 0, TypeCategory.IComponentData);
            // This must always be first so that Entity is always index 0 in the archetype
            m_Types[m_Count++] = new ComponentType(typeof(Entity), sizeof(Entity), TypeCategory.EntityData);
        }


        public static int GetTypeIndex<T>()
        {
            var typeIndex = StaticTypeLookup<T>.typeIndex;
            if (typeIndex != 0)
                return typeIndex;

            typeIndex = GetTypeIndex (typeof(T));
            StaticTypeLookup<T>.typeIndex = typeIndex;
            return typeIndex;
        }

        public static int GetTypeIndex(Type type)
        {
            var index = FindTypeIndex(type, m_Count);
            return index != -1 ? index : CreateTypeIndexThreadSafe(type);
        }

        private static int FindTypeIndex(Type type, int count)
        {
            for (var i = 0; i != count; i++)
            {
                var c = m_Types[i];
                if (c.Type == type)
                    return i;
            }
            return -1;
        }

        private static int CreateTypeIndexThreadSafe(Type type)
        {
            var lockTaken = false;
            try
            {
                m_CreateTypeLock.Enter(ref lockTaken);

                // After taking the lock, make sure the type hasn't been created
                // after doing the non-atomic FindTypeIndex
                var index = FindTypeIndex(type, m_Count);
                if (index != -1)
                    return index;

                var compoentType = BuildComponentType(type);

                index = m_Count++;
                m_Types[index] = compoentType;

                return index;
            }
            finally
            {
                if (lockTaken)
                    m_CreateTypeLock.Exit(true);
            }
        }

        private static ComponentType BuildComponentType(Type type)
        {
            var componentSize = 0;
            TypeCategory category;

            if (typeof(IComponentData).IsAssignableFrom(type))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (type.IsClass)
                    throw new ArgumentException($"{type} is an IComponentData, and thus must be a struct.");
                if (!UnsafeUtility.IsBlittable(type))
                    throw new ArgumentException($"{type} is an IComponentData, and thus must be blittable (No managed object is allowed on the struct).");
#endif

                category = TypeCategory.IComponentData;
                componentSize = UnsafeUtility.SizeOf(type);
            }
            else if (typeof(ISharedComponentData).IsAssignableFrom(type))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (type.IsClass)
                    throw new ArgumentException($"{type} is an ISharedComponentData, and thus must be a struct.");
#endif

                category = TypeCategory.ISharedComponentData;
            }
            else if (type.IsValueType)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!UnsafeUtility.IsBlittable(type))
                    throw new ArgumentException($"{type} is used for FixedArrays, and thus must be blittable.");
#endif
                category = TypeCategory.OtherValueType;
                componentSize = UnsafeUtility.SizeOf(type);
            }
            else if (type.IsClass)
            {
                category = TypeCategory.Class;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (type == typeof(GameObjectEntity))
                    throw new ArgumentException("GameObjectEntity can not be used from EntityManager. The component is ignored when creating entities for a GameObject.");
#endif
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            else
            {
                throw new ArgumentException($"'{type}' is not a valid component");
            }
#else
            else
            {
                category = TypeCategory.OtherValueType;
            }
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (typeof(IComponentData).IsAssignableFrom(type) && typeof(ISharedComponentData).IsAssignableFrom(type))
                throw new ArgumentException($"Component {type} can not be both IComponentData & ISharedComponentData");
#endif
            return new ComponentType(type, componentSize, category);
        }

        public static bool IsValidComponentTypeForArchetype(int typeIndex, bool isArray)
        {
            if (m_Types[typeIndex].Category == TypeCategory.OtherValueType)
                return isArray;
            return !isArray;
        }

        public static ComponentType GetComponentType(int typeIndex)
        {
            return m_Types[typeIndex];
        }

        public static ComponentType GetComponentType<T>()
        {
            return m_Types[GetTypeIndex<T>()];
        }

        public static Type GetType(int typeIndex)
        {
            return m_Types[typeIndex].Type;
        }

        public static int GetTypeCount()
        {
            return m_Count;
        }
    }
}

