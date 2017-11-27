using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS.Experimental.Slow
{
    class ComponentGroupEnumeratorStaticCache
    {
        public int[]                 ComponentDataFieldOffsets;
        public ComponentType[]       ComponentDataTypes;

        public int[]                 ComponentFieldOffsets;
        public ComponentType[]       ComponentTypes;

        public ComponentType[]       AllComponentTypes;
        
        public ComponentGroupEnumeratorStaticCache(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var componentFieldOffsetsBuilder = new List<int>();
            var componentTypesBuilder = new List<ComponentType>();
            
            var componentDataFieldOffsetsBuilder = new List<int>();
            var componentDataTypesBuilder = new List<ComponentType>();
            
            foreach(var field in fields)
            {
                var fieldType = field.FieldType;
                var offset = UnsafeUtility.GetFieldOffset(field);

                if (fieldType.IsPointer)
                {
                    //@TODO: Find out if there is a non-string based version of doing this...
                    string pointerTypeFullName = fieldType.FullName;
                    Type valueType = fieldType.Assembly.GetType(pointerTypeFullName.Remove(pointerTypeFullName.Length - 1));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (!typeof(IComponentData).IsAssignableFrom(valueType))
                        throw new System.ArgumentException($"{type}.{field.Name} is a pointer type but not a IComponentData. Only IComponentData may be a pointer type for enumeration.");
#endif                    
                    componentDataFieldOffsetsBuilder.Add(offset);
                    componentDataTypesBuilder.Add(valueType);
                }
                else if (fieldType.IsSubclassOf(typeof(Component)))
                {
                    componentFieldOffsetsBuilder.Add(offset);
                    componentTypesBuilder.Add(fieldType);
                }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                else if (typeof(IComponentData).IsAssignableFrom(fieldType))
                {
                    throw new System.ArgumentException($"{type}.{field.Name} must be an unsafe pointer to the {fieldType}. Like this: {fieldType}* {field.Name};");
                }
                else
                {
                    throw new System.ArgumentException($"{type}.{field.Name} can not be used in a component enumerator");
                }
#endif
            }

            ComponentFieldOffsets = componentFieldOffsetsBuilder.ToArray();
            ComponentTypes = componentTypesBuilder.ToArray();

            ComponentDataFieldOffsets = componentDataFieldOffsetsBuilder.ToArray();
            ComponentDataTypes = componentDataTypesBuilder.ToArray();

            componentTypesBuilder.AddRange(componentDataTypesBuilder);
            AllComponentTypes = componentTypesBuilder.ToArray();
        }
    }

    public struct ComponentGroupEnumerable<T> : IDisposable where T : struct 
    {
        ComponentGroup                         m_ComponentGroup;
        ComponentGroupEnumeratorStaticCache    m_StaticCache;

        ComponentDataArrayCache[]              m_ComponentDataCache;
        ComponentDataArrayCache[]              m_ComponentCache;
        
        public ComponentGroupEnumerable(EntityManager entityManager)
        {
            m_StaticCache = new ComponentGroupEnumeratorStaticCache(typeof(T));
            m_ComponentGroup = entityManager.CreateComponentGroup(m_StaticCache.AllComponentTypes);

            m_ComponentDataCache = new ComponentDataArrayCache[m_StaticCache.ComponentDataTypes.Length];
            m_ComponentCache = new ComponentDataArrayCache[m_StaticCache.ComponentTypes.Length];
        }

        public void Dispose()
        {
            m_ComponentGroup.Dispose();
            m_ComponentGroup = null;
        }

        public ComponentGroupEnumerator<T> GetEnumerator()
        {
            int length = 0;
            int componentIndex;
            for (int i = 0; i < m_ComponentDataCache.Length; i++)
                m_ComponentDataCache[i] = m_ComponentGroup.GetComponentDataArrayCache(m_StaticCache.ComponentDataTypes[i].typeIndex, out length, out componentIndex);

            for (int i = 0; i < m_ComponentCache.Length; i++)
                m_ComponentCache[i] = m_ComponentGroup.GetComponentDataArrayCache(m_StaticCache.ComponentTypes[i].typeIndex, out length, out componentIndex);
            
            return new ComponentGroupEnumerator<T>(length, m_StaticCache, m_ComponentDataCache, m_ComponentCache, m_ComponentGroup.GetArchetypeManager());
        }
    
        public struct ComponentGroupEnumerator<T> : IEnumerator<T> where T : struct
        {
            int[]                       m_ComponentFieldOffsets;
            ComponentDataArrayCache[]   m_ComponentCaches;

            int[]                       m_ComponentDataFieldOffsets;
            ComponentDataArrayCache[]   m_ComponentDataCaches;

            ArchetypeManager		    m_ArchetypeManager;
    
            int                         m_Index;
            int                         m_Length;
    
            int                         m_CacheBeginIndex;
            int                         m_CacheEndIndex;

            internal ComponentGroupEnumerator(int length, ComponentGroupEnumeratorStaticCache staticCache, ComponentDataArrayCache[] componentDataCaches, ComponentDataArrayCache[] componentCaches, ArchetypeManager archetypeManager)
            {
                m_Length = length;
                m_Index = -1;
                m_CacheBeginIndex = 0;
                m_CacheEndIndex = 0;
                m_ArchetypeManager = archetypeManager;

                m_ComponentDataFieldOffsets = staticCache.ComponentDataFieldOffsets;
                m_ComponentDataCaches = componentDataCaches;
                
                m_ComponentFieldOffsets = staticCache.ComponentFieldOffsets;
                m_ComponentCaches = componentCaches;
            }

            public void Dispose()
            {
                //@TODO: Prevent multiple instances (error checks)... because of ComponentDataArrayCache[] being reused from ComponentGroupEnumerable
            }
    
            public void UpdateCache()
            {
                for (int i = 0; i != m_ComponentCaches.Length; i++)
                    m_ComponentCaches[i].UpdateCache(m_Index);
                for (int i = 0; i != m_ComponentDataCaches.Length; i++)
                    m_ComponentDataCaches[i].UpdateCache(m_Index);

                if (m_ComponentDataCaches.Length != 0)
                {
                    m_CacheBeginIndex = m_ComponentDataCaches[0].CachedBeginIndex;
                    m_CacheEndIndex = m_ComponentDataCaches[0].CachedEndIndex;
                }
                else
                {
                    m_CacheBeginIndex = m_ComponentCaches[0].CachedBeginIndex;
                    m_CacheEndIndex = m_ComponentCaches[0].CachedEndIndex;
                }
            }
    
            public bool MoveNext()
            {
                m_Index++;
    
                if (m_Index < m_CacheBeginIndex || m_Index >= m_CacheEndIndex)
                {
                    if (m_Index >= m_Length)
                        return false;
                    
                    UpdateCache();
                    return true;
                }
                else
                {
                    return true;
                }
            }
    
            public void Reset()
            {
                m_Index = -1;
            }

            unsafe public T Current
            {
                get
                {
                    T value = default(T);
                    byte* valuePtr = (byte*)UnsafeUtility.AddressOf(ref value);

                    for (int i = 0; i != m_ComponentCaches.Length; i++)
                    {
                        var component = m_ComponentCaches[i].GetManagedObject(m_ArchetypeManager, m_Index);
                        var valuePtrOffsetted = (valuePtr + m_ComponentFieldOffsets[i]);
                        UnsafeUtility.CopyObjectAddressToPtr(component, (IntPtr)valuePtrOffsetted);
                    }

                    for (int i = 0; i != m_ComponentDataCaches.Length; i++)
                    {
                        void* componentPtr = (void*)((byte*)m_ComponentDataCaches[i].CachedPtr + (m_ComponentDataCaches[i].CachedSizeOf * m_Index));
                        void** valuePtrOffsetted = (void**)(valuePtr + m_ComponentDataFieldOffsets[i]);

                        *valuePtrOffsetted = componentPtr;
                    }
    
                    return value;
                }
            }
    
            object IEnumerator.Current
            {
                get { return Current; }
            }
        }
    }
}