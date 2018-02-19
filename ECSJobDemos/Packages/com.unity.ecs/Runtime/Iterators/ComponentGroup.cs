using System;
using System.Collections.Generic;

using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using Transform = UnityEngine.Transform;
using Component = UnityEngine.Component;
using TransformAccessArray = UnityEngine.Jobs.TransformAccessArray;

namespace Unity.ECS
{
    public unsafe class ComponentGroup : IDisposable, IManagedObjectModificationListener
    {
        readonly EntityGroupData*             m_GroupData;
        readonly ComponentJobSafetyManager    m_SafetyManager;
        readonly ArchetypeManager             m_TypeManager;
        readonly EntityDataManager*           m_EntityDataManager;
        MatchingArchetypes*                   m_LastRegisteredListenerArchetype;

        TransformAccessArray                  m_Transforms;
        bool                                  m_TransformsDirty;
        int*                                  m_FilteredSharedComponents;

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal string                       DisallowDisposing = null;
        #endif

        internal ComponentGroup(EntityGroupData* groupData, ComponentJobSafetyManager safetyManager, ArchetypeManager typeManager, EntityDataManager* entityDataManager )
        {
            m_GroupData = groupData;
            m_SafetyManager = safetyManager;
            m_TypeManager = typeManager;
            m_TransformsDirty = true;
            m_LastRegisteredListenerArchetype = null;
            m_FilteredSharedComponents = null;
            m_EntityDataManager = entityDataManager;
        }

        public void Dispose()
        {
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (DisallowDisposing  != null)
                throw new System.ArgumentException(DisallowDisposing);
        #endif

            if (m_Transforms.IsCreated)
                m_Transforms.Dispose();

            if (m_LastRegisteredListenerArchetype != null)
            {
                var transformType = TypeManager.GetTypeIndex<Transform>();
                for (var type = m_GroupData->FirstMatchingArchetype; type != m_LastRegisteredListenerArchetype->Next; type = type->Next)
                {
                    var idx = ChunkDataUtility.GetIndexInTypeArray(type->Archetype, transformType);
                    m_TypeManager.RemoveManagedObjectModificationListener(type->Archetype, idx, this);
                }
            }

            if (m_FilteredSharedComponents == null)
                return;

            var filteredCount = m_FilteredSharedComponents[0];
            var filtered = m_FilteredSharedComponents + 1;

            for(var i=0; i<filteredCount; ++i)
            {
                var sharedComponentIndex = filtered[i * 2 + 1];
                m_TypeManager.GetSharedComponentDataManager().RemoveReference(sharedComponentIndex);
            }

            UnsafeUtility.Free(m_FilteredSharedComponents, Allocator.Temp);
        }
        public void OnManagedObjectModified()
        {
            m_TransformsDirty = true;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle GetSafetyHandle(int indexInComponentGroup)
        {
            var type = m_GroupData->RequiredComponents + indexInComponentGroup;
            var isReadOnly = type->AccessModeType == ComponentType.AccessMode.ReadOnly;
            return m_SafetyManager.GetSafetyHandle(type->TypeIndex, isReadOnly);
        }
#endif

        public bool IsEmpty
        {
            get
            {
                if (m_FilteredSharedComponents == null)
                {
                    for (var match = m_GroupData->FirstMatchingArchetype; match != null; match = match->Next)
                    {
                        if (match->Archetype->EntityCount > 0)
                            return false;
                    }

                    return true;
                }
                else
                {
                    for (var match = m_GroupData->FirstMatchingArchetype; match != null; match = match->Next)
                    {
                        if (match->Archetype->EntityCount <= 0)
                            continue;

                        var archeType = match->Archetype;
                        for (var c = (Chunk*) archeType->ChunkList.Begin; c != archeType->ChunkList.End; c = (Chunk*) c->ChunkListNode.Next)
                        {
                            if (!ComponentChunkIterator.ChunkMatchesFilter(match, c, m_FilteredSharedComponents))
                                continue;

                            if (c->Count > 0)
                                return false;
                        }
                    }

                    return true;
                }
            }
        }

        internal void GetComponentChunkIterator(out int outLength, out ComponentChunkIterator outIterator)
        {
            // Update the archetype segments
            var length = 0;
            MatchingArchetypes* first = null;
            Chunk* firstNonEmptyChunk = null;
            if (m_FilteredSharedComponents == null)
            {
                for (var match = m_GroupData->FirstMatchingArchetype; match != null; match = match->Next)
                {
                    if (match->Archetype->EntityCount > 0)
                    {
                        length += match->Archetype->EntityCount;
                        if (first == null)
                            first = match;
                    }
                }
                if (first != null)
                    firstNonEmptyChunk = (Chunk*)first->Archetype->ChunkList.Begin;
            }
            else
            {
                for (var match = m_GroupData->FirstMatchingArchetype; match != null; match = match->Next)
                {
                    if (match->Archetype->EntityCount <= 0)
                        continue;

                    var archeType = match->Archetype;
                    for (var c = (Chunk*)archeType->ChunkList.Begin; c != archeType->ChunkList.End; c = (Chunk*)c->ChunkListNode.Next)
                    {
                        if (!ComponentChunkIterator.ChunkMatchesFilter(match, c, m_FilteredSharedComponents))
                            continue;

                        if (c->Count <= 0)
                            continue;

                        length += c->Count;
                        if (first != null)
                            continue;

                        first = match;
                        firstNonEmptyChunk = c;
                    }
                }
            }

            outLength = length;

            outIterator = first == null
                ? new ComponentChunkIterator(null, 0, null, null)
                : new ComponentChunkIterator(first, length, firstNonEmptyChunk, m_FilteredSharedComponents);
        }

        internal int GetIndexInComponentGroup(int componentType)
        {
            var componentIndex = 0;
            while (componentIndex < m_GroupData->RequiredComponentsCount && m_GroupData->RequiredComponents[componentIndex].TypeIndex != componentType)
                ++componentIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (componentIndex >= m_GroupData->RequiredComponentsCount)
                throw new InvalidOperationException(
                    $"Trying to get iterator for {TypeManager.GetType(componentType)} but the required component type was not declared in the EntityGroup.");
#endif
            return componentIndex;
        }

        internal void GetIndexFromEntity(out IndexFromEntity output)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            output = new IndexFromEntity(m_EntityDataManager,m_GroupData,m_FilteredSharedComponents,  m_SafetyManager.GetSafetyHandle(TypeManager.GetTypeIndex<Entity>(), true));
#else
            output = new IndexFromEntity(m_EntityDataManager,m_GroupData,m_FilteredSharedComponents);
#endif
        }

        internal IndexFromEntity GetIndexFromEntity()
        {
            IndexFromEntity res;
            GetIndexFromEntity(out res);
            return res;
        }

        internal void GetComponentDataArray<T>(ref ComponentChunkIterator iterator, int indexInComponentGroup, int length, out ComponentDataArray<T> output) where T : struct, IComponentData
        {
            iterator.IndexInComponentGroup = indexInComponentGroup;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            output = new ComponentDataArray<T>(iterator, length, GetSafetyHandle(indexInComponentGroup));
#else
			output = new ComponentDataArray<T>(iterator, length);
#endif
        }

        public ComponentDataArray<T> GetComponentDataArray<T>() where T : struct, IComponentData
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            var indexInComponentGroup = GetIndexInComponentGroup(TypeManager.GetTypeIndex<T>());

            ComponentDataArray<T> res;
            GetComponentDataArray<T>(ref iterator, indexInComponentGroup, length, out res);
            return res;
        }

        //@TODO: THIS API IS NOT SAFE, unpublicify
        public ComponentDataArray<T> GetComponentDataArray<T>(ComponentType componentType) where T : struct, IComponentData
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            var indexInComponentGroup = GetIndexInComponentGroup(componentType.TypeIndex);

            ComponentDataArray<T> res;
            GetComponentDataArray<T>(ref iterator, indexInComponentGroup, length, out res);
            return res;
        }

        internal void GetSharedComponentDataArray<T>(ref ComponentChunkIterator iterator, int indexInComponentGroup, int length, out SharedComponentDataArray<T> output) where T : struct, ISharedComponentData
        {
            iterator.IndexInComponentGroup = indexInComponentGroup;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var typeIndex = m_GroupData->RequiredComponents[indexInComponentGroup].TypeIndex;
            output = new SharedComponentDataArray<T>(m_TypeManager.GetSharedComponentDataManager(), indexInComponentGroup, iterator, length, m_SafetyManager.GetSafetyHandle(typeIndex, true));
#else
			output = new SharedComponentDataArray<T>(m_TypeManager.GetSharedComponentDataManager(), indexInComponentGroup, iterator, length);
#endif
        }

        public SharedComponentDataArray<T> GetSharedComponentDataArray<T>() where T : struct, ISharedComponentData
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            var indexInComponentGroup = GetIndexInComponentGroup(TypeManager.GetTypeIndex<T>());

            SharedComponentDataArray<T> res;
            GetSharedComponentDataArray<T>(ref iterator, indexInComponentGroup, length, out res);
            return res;
        }

        internal void GetFixedArrayArray<T>(ref ComponentChunkIterator iterator, int indexInComponentGroup, int length, out FixedArrayArray<T> output) where T : struct
        {
            iterator.IndexInComponentGroup = indexInComponentGroup;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            output = new FixedArrayArray<T>(iterator, length, GetSafetyHandle(indexInComponentGroup));
#else
			output = new FixedArrayArray<T>(iterator, length);
#endif
        }

        public FixedArrayArray<T> GetFixedArrayArray<T>() where T : struct
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            var indexInComponentGroup = GetIndexInComponentGroup(TypeManager.GetTypeIndex<T>());

            FixedArrayArray<T> res;
            GetFixedArrayArray<T>(ref iterator, indexInComponentGroup, length, out res);
            return res;
        }

        internal void GetEntityArray(ref ComponentChunkIterator iterator, int length, out EntityArray output)
        {
            iterator.IndexInComponentGroup = 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //@TODO: Comment on why this has to be false...
            output = new EntityArray(iterator, length, m_SafetyManager.GetSafetyHandle(TypeManager.GetTypeIndex<Entity>(), false));
#else
			output = new EntityArray(iterator, length);
#endif
        }

        public EntityArray GetEntityArray()
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);

            EntityArray res;
            GetEntityArray(ref iterator, length, out res);
            return res;
        }

        internal void GetComponentArray<T>(ref ComponentChunkIterator iterator, int indexInComponentGroup, int length, out ComponentArray<T> output) where T : Component
        {
            iterator.IndexInComponentGroup = indexInComponentGroup;
            output = new ComponentArray<T>(iterator, length, m_TypeManager);
        }

        public ComponentArray<T> GetComponentArray<T>() where T : Component
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            var indexInComponentGroup = GetIndexInComponentGroup(TypeManager.GetTypeIndex<T>());


            ComponentArray<T> res;
            GetComponentArray<T>(ref iterator, indexInComponentGroup, length, out res);
            return res;
        }

        public GameObjectArray GetGameObjectArray()
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            iterator.IndexInComponentGroup = GetIndexInComponentGroup(TypeManager.GetTypeIndex<Transform>());
            return new GameObjectArray(iterator, length, m_TypeManager);
        }

        public int CalculateLength()
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            return length;
        }

        public TransformAccessArray GetTransformAccessArray()
        {
            var transformIdx = TypeManager.GetTypeIndex<Transform>();
            for (var type = m_LastRegisteredListenerArchetype != null ? m_LastRegisteredListenerArchetype->Next : m_GroupData->FirstMatchingArchetype; type != null; type = type->Next)
            {
                var idx = ChunkDataUtility.GetIndexInTypeArray(type->Archetype, transformIdx);
                m_TypeManager.AddManagedObjectModificationListener(type->Archetype, idx, this);
                m_TransformsDirty = true;
            }
            m_LastRegisteredListenerArchetype = m_GroupData->LastMatchingArchetype;

            if (m_TransformsDirty)
            {
                UnityEngine.Profiling.Profiler.BeginSample("DirtyTransformAccessArrayUpdate");
                var trans = GetComponentArray<Transform>();
                if (!m_Transforms.IsCreated)
                    m_Transforms = new TransformAccessArray(trans.ToArray());
                else
                    m_Transforms.SetTransforms(trans.ToArray());
                UnityEngine.Profiling.Profiler.EndSample();
            }

            m_TransformsDirty = false;
            return m_Transforms;
        }

        public bool CompareComponents(ComponentType[] componentTypes)
        {
            fixed (ComponentType* ptr = componentTypes)
            {
                return CompareComponents(ptr, componentTypes.Length);     
            }
        }

        internal bool CompareComponents(ComponentType* componentTypes, int count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (var k = 0; k < count; ++k)
            {
                if (componentTypes[k].TypeIndex == TypeManager.GetTypeIndex<Entity>())
                    throw new System.ArgumentException("ComponentGroup.CompareComponents may not include typeof(Entity), it is implicit");
            }
#endif

            // ComponentGroups are constructed including the Entity ID
            int requiredCount = m_GroupData->RequiredComponentsCount; 
            if (count != requiredCount - 1)
                return false;

            for (var k = 0; k < count; ++k)
            {
                int i;
                for (i = 1; i < requiredCount ; ++i)
                {
                    if (m_GroupData->RequiredComponents[i] == componentTypes[k])
                        break;
                }

                if (i == requiredCount)
                    return false;
            }

            return true;
        }

        public Type[] Types
        {
            get
            {
                var types = new List<Type> ();
                for (var i = 0; i < m_GroupData->RequiredComponentsCount; ++i)
                {
                    if (m_GroupData->RequiredComponents[i].AccessModeType != ComponentType.AccessMode.Subtractive)
                        types.Add(TypeManager.GetType(m_GroupData->RequiredComponents[i].TypeIndex));
                }

                return types.ToArray ();
            }
        }

        public ComponentGroup GetVariation<SharedComponent1>(SharedComponent1 sharedComponent1)
            where SharedComponent1 : struct, ISharedComponentData
        {
            var variationComponentGroup = new ComponentGroup(m_GroupData, m_SafetyManager, m_TypeManager, m_EntityDataManager);

            var componetIndex1 = GetIndexInComponentGroup(TypeManager.GetTypeIndex<SharedComponent1>());
            const int filteredCount = 1;

            var filtered = (int*)UnsafeUtility.Malloc((filteredCount * 2 + 1) * sizeof(int), sizeof(int), Allocator.Temp); // TODO: does temp allocator make sense here?
            variationComponentGroup.m_FilteredSharedComponents = filtered;


            filtered[0] = filteredCount;
            filtered[1] = componetIndex1;
            filtered[2] = m_TypeManager.GetSharedComponentDataManager().InsertSharedComponent(sharedComponent1);

            return variationComponentGroup;
        }

        public ComponentGroup GetVariation<SharedComponent1, SharedComponent2>(SharedComponent1 sharedComponent1, SharedComponent2 sharedComponent2)
            where SharedComponent1 : struct, ISharedComponentData
            where SharedComponent2 : struct, ISharedComponentData
        {
            var variationComponentGroup = new ComponentGroup(m_GroupData, m_SafetyManager, m_TypeManager, m_EntityDataManager);

            var componetIndex1 = GetIndexInComponentGroup(TypeManager.GetTypeIndex<SharedComponent1>());
            var componetIndex2 = GetIndexInComponentGroup(TypeManager.GetTypeIndex<SharedComponent2>());
            const int filteredCount = 2;

            var filtered = (int*)UnsafeUtility.Malloc((filteredCount * 2 + 1) * sizeof(int), sizeof(int), Allocator.Temp);
            variationComponentGroup.m_FilteredSharedComponents = filtered;

            filtered[0] = filteredCount;
            filtered[1] = componetIndex1;
            filtered[2] = m_TypeManager.GetSharedComponentDataManager().InsertSharedComponent(sharedComponent1);
            filtered[3] = componetIndex2;
            filtered[4] = m_TypeManager.GetSharedComponentDataManager().InsertSharedComponent(sharedComponent2);

            return variationComponentGroup;
        }

        public void CompleteDependency()
        {
            m_SafetyManager.CompleteDependencies(m_GroupData->ReaderTypes, m_GroupData->ReaderTypesCount, m_GroupData->WriterTypes, m_GroupData->WriterTypesCount);
        }

        public JobHandle GetDependency()
        {
            return m_SafetyManager.GetDependency(m_GroupData->ReaderTypes, m_GroupData->ReaderTypesCount, m_GroupData->WriterTypes, m_GroupData->WriterTypesCount);
        }

        public void AddDependency(JobHandle job)
        {
            m_SafetyManager.AddDependency(m_GroupData->ReaderTypes, m_GroupData->ReaderTypesCount, m_GroupData->WriterTypes, m_GroupData->WriterTypesCount, job);
        }

        internal ArchetypeManager GetArchetypeManager()
        {
            return m_TypeManager;
        }
    }
}
