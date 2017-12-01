using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    class ComponentGroupArrayStaticCache
    {
        public ComponentType[]       ComponentTypes;
        public int[]                 ComponentFieldOffsets;
        public int                   ComponentDataCount;
        public int                   ComponentCount;
        
        public ComponentGroupArrayStaticCache(Type type)
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (componentTypesBuilder.Count + componentDataTypesBuilder.Count > ComponentGroupArrayData.kMaxStream)
            {
                throw new System.ArgumentException($"{type} has too many component references. A ComponentGroup Array can have up to {ComponentGroupArrayData.kMaxStream}.");
            }
#endif

            ComponentDataCount = componentDataTypesBuilder.Count;
            ComponentCount = componentTypesBuilder.Count;
            
            componentDataTypesBuilder.AddRange(componentTypesBuilder);
            ComponentTypes = componentDataTypesBuilder.ToArray();

            componentDataFieldOffsetsBuilder.AddRange(componentFieldOffsetsBuilder);
            ComponentFieldOffsets = componentDataFieldOffsetsBuilder.ToArray();
        }
    }
    
    unsafe struct ComponentGroupArrayData
    {
        public const int kMaxStream = 6;

        struct ComponentGroupStream
        {
            public byte*     CachedPtr;
            public int       SizeOf;
            public ushort    FieldOffset;
            public ushort    TypeIndexInArchetype;
        }
    
        fixed byte                  m_Caches[16 * kMaxStream];

        int                         m_ComponentDataCount;
        int                         m_ComponentCount;

        public int                  Length;
        public int                  CacheBeginIndex;
        public int                  CacheEndIndex;

        ComponentChunkIterator      m_ChunkIterator;
        fixed int                   m_ComponentTypes[kMaxStream];

        ArchetypeManager            m_ArchetypeManager;

        public ComponentGroupArrayData(int length, ComponentGroupArrayStaticCache staticCache, ComponentChunkIterator chunkIterator, ArchetypeManager archetypeManager)
        {
            Length = length;
            CacheBeginIndex = 0;
            CacheEndIndex = 0;
            m_ArchetypeManager = archetypeManager;

            m_ChunkIterator = chunkIterator;

            m_ComponentDataCount = staticCache.ComponentDataCount;
            m_ComponentCount = staticCache.ComponentCount;

            fixed (int* componentTypes = m_ComponentTypes)
            {
                fixed (byte* cacheBytes = m_Caches)
                {
                    ComponentGroupStream* streams = (ComponentGroupStream*)cacheBytes;
                        
                    for (int i = 0; i < staticCache.ComponentDataCount + staticCache.ComponentCount; i++)
                    {
                        componentTypes[i] = staticCache.ComponentTypes[i].typeIndex;
                        streams[i].FieldOffset = (ushort)staticCache.ComponentFieldOffsets[i];
                    }
                }
            }
        }

        public void UpdateCache(int index)
        {
            ComponentChunkCache cache;
            m_ChunkIterator.UpdateCache(index, out cache);

            CacheBeginIndex = cache.CachedBeginIndex;
            CacheEndIndex = cache.CachedEndIndex;

            fixed (int* componentTypes = m_ComponentTypes)
            {
                fixed (byte* cacheBytes = m_Caches)
                {
                    ComponentGroupStream* streams = (ComponentGroupStream*)cacheBytes;
                    int totalCount = m_ComponentDataCount + m_ComponentCount;
                    for (int i = 0; i < totalCount; i++)
                    {
                        int indexInArcheType;
                        m_ChunkIterator.GetCacheForType(componentTypes[i], out cache, out indexInArcheType);
                        streams[i].SizeOf = cache.CachedSizeOf;
                        streams[i].CachedPtr = (byte*)cache.CachedPtr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if (indexInArcheType > ushort.MaxValue)
                            throw new System.ArgumentException($"There is a maximum of {ushort.MaxValue} components on one entity.");
#endif
                        streams[i].TypeIndexInArchetype = (ushort)indexInArcheType;
                    }
                }
            }        
        }

        public void PatchPtrs(int index, byte* valuePtr)
        {
            fixed (byte* cacheBytes = m_Caches)
            {
                ComponentGroupStream* streams = (ComponentGroupStream*)cacheBytes;
                for (int i = 0; i != m_ComponentDataCount; i++)
                {
                    void* componentPtr = (void*)(streams[i].CachedPtr + (streams[i].SizeOf * index));
                    void** valuePtrOffsetted = (void**)(valuePtr + streams[i].FieldOffset);

                    *valuePtrOffsetted = componentPtr;
                }

                for (int i = m_ComponentDataCount; i != m_ComponentDataCount + m_ComponentCount; i++)
                {
                    var component = m_ChunkIterator.GetManagedObject(m_ArchetypeManager, streams[i].TypeIndexInArchetype, CacheBeginIndex, index);
                    var valuePtrOffsetted = (valuePtr + streams[i].FieldOffset);
                    UnsafeUtility.CopyObjectAddressToPtr(component, (IntPtr)valuePtrOffsetted);
                }
            }
        }
    }

    public class ComponentGroupArray<T> : IDisposable where T : struct 
    {
        ComponentGroup                    m_ComponentGroup;
        ComponentGroupArrayStaticCache    m_StaticCache;
        
        public ComponentGroupArray(EntityManager entityManager)
        {
            m_StaticCache = new ComponentGroupArrayStaticCache(typeof(T));
            m_ComponentGroup = entityManager.CreateComponentGroup(m_StaticCache.ComponentTypes);
        }

        public void Dispose()
        {
            m_ComponentGroup.Dispose();
            m_ComponentGroup = null;
        }
        
        public ComponentGroup ComponentGroup { get { return m_ComponentGroup; } }

        public ComponentGroupEnumerator<T> GetEnumerator()
        {
            int length = 0;
            int componentIndex;
            var iterator = m_ComponentGroup.GetComponentChunkIterator(m_StaticCache.ComponentTypes[0].typeIndex, out length, out componentIndex);
            
            return new ComponentGroupEnumerator<T>(length, m_StaticCache, iterator, m_ComponentGroup.GetArchetypeManager());
        }
        
        //@TODO: Container & safety...
        public unsafe struct ComponentGroupEnumerator<T> : IEnumerator<T> where T : struct
        {
            ComponentGroupArrayData     m_Data;
            int                         m_Index;
    
            internal ComponentGroupEnumerator(int length, ComponentGroupArrayStaticCache staticCache, ComponentChunkIterator chunkIterator, ArchetypeManager archetypeManager)
            {
                m_Data = new ComponentGroupArrayData(length, staticCache, chunkIterator, archetypeManager);
                m_Index = -1;
            }

            public void Dispose()
            {
                //@TODO: Prevent multiple instances (error checks)... because of ComponentChunkIterator[] being reused from ComponentGroupEnumerable
            }

            public int Length { get { return m_Data.Length; } }

            public bool MoveNext()
            {
                m_Index++;
    
                if (m_Index < m_Data.CacheBeginIndex || m_Index >= m_Data.CacheEndIndex)
                {
                    if (m_Index >= m_Data.Length)
                        return false;
                    m_Data.UpdateCache(m_Index);
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
                    var value = default(T);
                    byte* valuePtr = (byte*)UnsafeUtility.AddressOf(ref value);
                    m_Data.PatchPtrs(m_Index, valuePtr);
                    return value;
                }
            }
            
            //@TODO: Wrong place, not very C#py
            public unsafe T this[int index]
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
//                  AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
//                  if (index < m_MinIndex || index > m_MaxIndex)
//                      FailOutOfRangeError(index);
#endif

                    m_Index = index;
                    if (m_Index < m_Data.CacheBeginIndex || m_Index >= m_Data.CacheEndIndex)
                        m_Data.UpdateCache(m_Index);

                    return Current;
                }
            }        
            
    
            object IEnumerator.Current
            {
                get { return Current; }
            }
        }
    }
}