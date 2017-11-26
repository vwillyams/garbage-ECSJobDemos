using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine.ECS.Experimental.Slow
{
    class ComponentGroupEnumeratorStaticCache
    {
        public FieldInfo[]           Fields;
        public ComponentType[]       ComponentTypes;

        public ComponentGroupEnumeratorStaticCache(Type type)
        {
            Fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            ComponentTypes = new ComponentType[Fields.Length];
            for (int i = 0; i != Fields.Length; i++)
            {
                var fieldType = Fields[i].FieldType;
                if (!fieldType.IsSubclassOf(typeof(Component)))
                    throw new System.ArgumentException($"{type}.{Fields[i].Name} must be a class UnityEngine.Component. IComponentData is not supported for now until C# 7.2 with ref structs support.");
                ComponentTypes[i] = new ComponentType(fieldType);
            }
        }
    }

    public struct ComponentGroupEnumerable<T> : IDisposable where T : struct 
    {
        ComponentGroup                         m_ComponentGroup;
        ComponentGroupEnumeratorStaticCache    m_StaticCache;

        public ComponentGroupEnumerable(EntityManager entityManager)
        {
            m_StaticCache = new ComponentGroupEnumeratorStaticCache(typeof(T));
            m_ComponentGroup = entityManager.CreateComponentGroup(m_StaticCache.ComponentTypes);
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
            var caches = new ComponentDataArrayCache[m_StaticCache.ComponentTypes.Length];
            for (int i = 0; i < caches.Length; i++)
                caches[i] = m_ComponentGroup.GetComponentDataArrayCache(m_StaticCache.ComponentTypes[i].typeIndex, out length, out componentIndex);

            return new ComponentGroupEnumerator<T>(length, m_StaticCache, caches, m_ComponentGroup.GetArchetypeManager());
        }
    
        public struct ComponentGroupEnumerator<T> : IEnumerator<T> where T : struct
        {
            FieldInfo[]                 m_Fields;
            ComponentDataArrayCache[]   m_DataArrayCaches;
    
            ArchetypeManager		    m_ArchetypeManager;
    
            int                         m_Index;
            int                         m_Length;
    
            int                         m_CacheBeginIndex;
            int                         m_CacheEndIndex;

            internal ComponentGroupEnumerator(int length, ComponentGroupEnumeratorStaticCache staticCache, ComponentDataArrayCache[] dataArrays, ArchetypeManager archetypeManager)
            {
                m_Length = length;
                m_Index = -1;
                m_CacheBeginIndex = 0;
                m_CacheEndIndex = 0;
                m_ArchetypeManager = archetypeManager;
                
                m_Fields = staticCache.Fields;
                m_DataArrayCaches = dataArrays;
            }
    
            public void Dispose()
            {
                m_Fields = null;
                m_DataArrayCaches = null;
            }
    
            public void UpdateCache()
            {
                for (int i = 0; i != m_DataArrayCaches.Length; i++)
                    m_DataArrayCaches[i].UpdateCache(m_Index);
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
    
            public T Current
            {
                get
                {
                    object value = new T();
                    
                    //@TODO: This could use a bit of optimization...
                    // * no gc alloc
                    // * write to pointer directly (need il asm?)
                    for (int i = 0; i != m_DataArrayCaches.Length; i++)
                    {
                        var component = m_DataArrayCaches[i].GetManagedObject(m_ArchetypeManager, m_Index);
                        m_Fields[i].SetValue(value, component);
                    }
    
                    return (T)value;
                }
            }
    
            object IEnumerator.Current
            {
                get { return Current; }
            }
        }
    }



}