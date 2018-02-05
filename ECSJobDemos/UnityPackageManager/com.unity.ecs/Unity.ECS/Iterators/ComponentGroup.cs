using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
    unsafe class EntityGroupManager : IDisposable
    {
        NativeMultiHashMap<uint, IntPtr>    m_GroupLookup;
        ChunkAllocator                      m_GroupDataChunkAllocator;
        EntityGroupData*                    m_LastGroupData;
        ComponentJobSafetyManager           m_JobSafetyManager;

        public EntityGroupManager(ComponentJobSafetyManager safetyManager)
        {
            m_GroupLookup = new NativeMultiHashMap<uint, IntPtr>(256, Allocator.Persistent);
            m_JobSafetyManager = safetyManager;
        }


        public ComponentGroup CreateEntityGroup(ArchetypeManager typeMan, ComponentType* requiredTypes, int requiredCount, TransformAccessArray trans)
        {
            uint hash = HashUtility.fletcher32((ushort*)requiredTypes, requiredCount * sizeof(ComponentType) / sizeof(short));
            NativeMultiHashMapIterator<uint> it;
            IntPtr grpPtr;
            EntityGroupData* grp;
            if (m_GroupLookup.TryGetFirstValue(hash, out grpPtr, out it))
            {
                do
                {
                    grp = (EntityGroupData*)grpPtr;
                    if (ComponentType.CompareArray(grp->requiredComponents, grp->requiredComponentsCount, requiredTypes, requiredCount))
                        return new ComponentGroup(grp, m_JobSafetyManager, typeMan);
                }
                while (m_GroupLookup.TryGetNextValue(out grpPtr, ref it));
            }

            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            grp = (EntityGroupData*)m_GroupDataChunkAllocator.Allocate(sizeof(EntityGroupData), 8);
            grp->prevGroup = m_LastGroupData;
            m_LastGroupData = grp;
            grp->requiredComponentsCount = requiredCount;
            grp->requiredComponents = (ComponentType*)m_GroupDataChunkAllocator.Construct(sizeof(ComponentType) * requiredCount, 4, requiredTypes);

            grp->readerTypesCount = 0;
            grp->writerTypesCount = 0;

            grp->subtractiveComponentsCount = 0;

            for (int i = 0; i != requiredCount;i++)
            {
                if (requiredTypes[i].accessMode == ComponentType.AccessMode.Subtractive)
                    grp->subtractiveComponentsCount++;
                else if (requiredTypes[i].accessMode == ComponentType.AccessMode.ReadOnly)
                    grp->readerTypesCount++;
                else
                    grp->writerTypesCount++;
            }
            grp->readerTypes = (int*)m_GroupDataChunkAllocator.Allocate(sizeof(int) * grp->readerTypesCount, 4);
            grp->writerTypes = (int*)m_GroupDataChunkAllocator.Allocate(sizeof(int) * grp->writerTypesCount, 4);

            int curReader = 0;
            int curWriter = 0;
            for (int i = 0; i != requiredCount;i++)
            {
                if (requiredTypes[i].accessMode == ComponentType.AccessMode.Subtractive)
                {}
                else if (requiredTypes[i].accessMode == ComponentType.AccessMode.ReadOnly)
                    grp->readerTypes[curReader++] = requiredTypes[i].typeIndex;
                else
                    grp->writerTypes[curWriter++] = requiredTypes[i].typeIndex;
            }

            grp->requiredComponents = (ComponentType*)m_GroupDataChunkAllocator.Construct(sizeof(ComponentType) * requiredCount, 4, requiredTypes);

            grp->firstMatchingArchetype = null;
            grp->lastMatchingArchetype = null;
            for (Archetype* type = typeMan.m_LastArchetype; type != null; type = type->prevArchetype)
                AddArchetypeIfMatching(type, grp);
            m_GroupLookup.Add(hash, (IntPtr)grp);
            return new ComponentGroup(grp, m_JobSafetyManager, typeMan);
        }
        public void Dispose()
        {
            //@TODO: Need to wait for all job handles to be completed..

            m_GroupLookup.Dispose();
            m_GroupDataChunkAllocator.Dispose();
        }
        internal void OnArchetypeAdded(Archetype* type)
        {
            for (EntityGroupData* grp = m_LastGroupData; grp != null; grp = grp->prevGroup)
                AddArchetypeIfMatching(type, grp);
        }

        void AddArchetypeIfMatching(Archetype* archetype, EntityGroupData* group)
        {
            if (group->requiredComponentsCount - group->subtractiveComponentsCount > archetype->typesCount)
                return;
            int typeI = 0;
            int prevTypeI = 0;
            for (int i = 0; i < group->requiredComponentsCount; ++i, ++typeI)
            {
                while (archetype->types[typeI].typeIndex < group->requiredComponents[i].typeIndex && typeI < archetype->typesCount)
                    ++typeI;

                bool hasComponent = true;
                if (typeI >= archetype->typesCount)
                    hasComponent = false;

                // Type mismatch
                if (hasComponent && archetype->types[typeI].typeIndex != group->requiredComponents[i].typeIndex)
                    hasComponent = false;

                if (hasComponent && group->requiredComponents[i].accessMode == ComponentType.AccessMode.Subtractive)
                    return;
                if (!hasComponent && group->requiredComponents[i].accessMode != ComponentType.AccessMode.Subtractive)
                    return;
                if (hasComponent)
                    prevTypeI = typeI;
                else
                    typeI = prevTypeI;
            }
            MatchingArchetypes* match = (MatchingArchetypes*)m_GroupDataChunkAllocator.Allocate(MatchingArchetypes.GetAllocationSize(group->requiredComponentsCount), 8);
            match->archetype = archetype;
            var typeIndexInArchetypeArray = match->typeIndexInArchetypeArray;

            if (group->lastMatchingArchetype == null)
                group->lastMatchingArchetype = match;

            match->next = group->firstMatchingArchetype;
            group->firstMatchingArchetype = match;

            for (int component = 0; component < group->requiredComponentsCount; ++component)
            {
                int typeComponentIndex = -1;
                if (group->requiredComponents[component].accessMode != ComponentType.AccessMode.Subtractive)
                {
                    typeComponentIndex = ChunkDataUtility.GetIndexInTypeArray(archetype, group->requiredComponents[component].typeIndex);
                    Assertions.Assert.AreNotEqual(-1, typeComponentIndex);
                }

                typeIndexInArchetypeArray[component] = typeComponentIndex;
            }

        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct MatchingArchetypes
    {
        public Archetype*                       archetype;
        public MatchingArchetypes*              next;
        public fixed int                        typeIndexInArchetypeArray[1];

        public static int GetAllocationSize(int requiredComponentsCount)
        {
            return sizeof(MatchingArchetypes) + sizeof(int) * (requiredComponentsCount - 1);
        }
    }

    unsafe struct EntityGroupData
    {
        public int*                 readerTypes;
        public int                  readerTypesCount;

        public int*                 writerTypes;
        public int                  writerTypesCount;

        public ComponentType*       requiredComponents;
        public int                  requiredComponentsCount;
        public int                  subtractiveComponentsCount;
        public MatchingArchetypes*  firstMatchingArchetype;
        public MatchingArchetypes*  lastMatchingArchetype;
        public EntityGroupData*     prevGroup;
    }

    //@TODO: Make safe when entity manager is destroyed.
    //@TODO: This needs to become a struct

    public unsafe class ComponentGroup : IDisposable, IManagedObjectModificationListener
    {
        EntityGroupData*                      m_GroupData;
        ComponentJobSafetyManager             m_SafetyManager;
        ArchetypeManager                      m_TypeManager;
        MatchingArchetypes*                   m_LastRegisteredListenerArchetype;

        TransformAccessArray                  m_Transforms;
        bool                                  m_TransformsDirty;
		int*                                  m_filteredSharedComponents;

        internal ComponentGroup(EntityGroupData* groupData, ComponentJobSafetyManager safetyManager, ArchetypeManager typeManager)
        {
            m_GroupData = groupData;
            m_SafetyManager = safetyManager;
            m_TypeManager = typeManager;
            m_TransformsDirty = true;
            m_LastRegisteredListenerArchetype = null;
            m_filteredSharedComponents = null;
        }

        public void Dispose()
        {
            if (m_Transforms.IsCreated)
            {
                m_Transforms.Dispose();
            }

            if (m_LastRegisteredListenerArchetype != null)
            {
                var transformType = TypeManager.GetTypeIndex<Transform>();
                for (MatchingArchetypes* type = m_GroupData->firstMatchingArchetype; type != m_LastRegisteredListenerArchetype->next; type = type->next)
                {
                    int idx = ChunkDataUtility.GetIndexInTypeArray(type->archetype, transformType);
                    m_TypeManager.RemoveManagedObjectModificationListener(type->archetype, idx, this);
                }
            }

            if (m_filteredSharedComponents != null)
            {
                int filteredCount = m_filteredSharedComponents[0];
                var filtered = m_filteredSharedComponents + 1;

                for(int i=0; i<filteredCount; ++i)
                {
                    int sharedComponentIndex = filtered[i * 2 + 1];
                    m_TypeManager.GetSharedComponentDataManager().RemoveReference(sharedComponentIndex);
                }

                UnsafeUtility.Free(m_filteredSharedComponents, Allocator.Temp);
            }
        }
        public void OnManagedObjectModified()
        {
            m_TransformsDirty = true;
        }

        bool IsReadOnly(int componentIndex)
        {
            return m_GroupData->requiredComponents[componentIndex].accessMode == ComponentType.AccessMode.ReadOnly;
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle GetSafetyHandle(int componentType, bool isReadOnly)
        {
            return m_SafetyManager.GetSafetyHandle(componentType, isReadOnly);
        }
        #endif

        internal static void AddReaderWriter(ComponentType type, List<int> reading, List<int> writing)
        {
            if (!type.RequiresJobDependency)
                return;

            if (type.accessMode == ComponentType.AccessMode.ReadOnly)
            {
                if (reading.Contains(type.typeIndex))
                    return;
                if (writing.Contains(type.typeIndex))
                    return;

                reading.Add(type.typeIndex);
            }
            else
            {
                if (reading.Contains(type.typeIndex))
                    reading.Remove(type.typeIndex);
                if (writing.Contains(type.typeIndex))
                    return;
                writing.Add(type.typeIndex);
            }
        }


        internal static void ExtractJobDependencyTypes(ComponentGroup[] groups, List<int> reading, List<int> writing)
        {
            foreach (var group in groups)
            {
                for (int i = 0;i != group.m_GroupData->requiredComponentsCount;i++)
                {
                    ComponentType type = group.m_GroupData->requiredComponents[i];
                    AddReaderWriter(type, reading, writing);
                }
            }
        }

        internal ComponentChunkIterator GetComponentChunkIterator(int componentType, out int outLength, out int componentIndex)
        {
            componentIndex = 0;
            while (componentIndex < m_GroupData->requiredComponentsCount && m_GroupData->requiredComponents[componentIndex].typeIndex != componentType)
                ++componentIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (componentIndex >= m_GroupData->requiredComponentsCount)
                throw new InvalidOperationException(string.Format("Trying to get ComponentDataArray for {0} but the required component type was not declared in the EntityGroup.", TypeManager.GetType(componentType)));
#endif

            // Update the archetype segments
            int length = 0;
            MatchingArchetypes* first = null;
            Chunk* firstNonEmptyChunk = null;
            if (m_filteredSharedComponents == null)
            {
                for (var match = m_GroupData->firstMatchingArchetype; match != null; match = match->next)
                {
                    if (match->archetype->entityCount > 0)
                    {
                        length += match->archetype->entityCount;
                        if (first == null)
                            first = match;
                    }
                }
                if(first != null)
                    firstNonEmptyChunk = (Chunk*)first->archetype->chunkList.Begin();
            }
            else
            {
                for (var match = m_GroupData->firstMatchingArchetype; match != null; match = match->next)
                {
                    if (match->archetype->entityCount > 0)
                    {
                        var archeType = match->archetype;
                        for (Chunk* c = (Chunk*)archeType->chunkList.Begin(); c != archeType->chunkList.End(); c = (Chunk*)c->chunkListNode.next)
                        {
                            if (ComponentChunkIterator.ChunkMatchesFilter(match, c, m_filteredSharedComponents))
                            {
                                if (c->count > 0)
                                {
                                    length += c->count;
                                    if (first == null)
                                    {
                                        first = match;
                                        firstNonEmptyChunk = c;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            outLength = length;

            if (first == null)
                return new ComponentChunkIterator(null, 0, 0, null, null);
            return new ComponentChunkIterator(first, componentIndex, length, firstNonEmptyChunk, m_filteredSharedComponents);
        }

        public ComponentDataArray<T> GetComponentDataArray<T>() where T : struct, IComponentData
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<T>();

            var cache = GetComponentChunkIterator(typeIndex, out length, out componentIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ComponentDataArray<T>(cache, length, m_SafetyManager.GetSafetyHandle(typeIndex, IsReadOnly(componentIndex)));
#else
			return new ComponentDataArray<T>(cache, length);
#endif
        }

        public FixedArrayArray<T> GetFixedArrayArray<T>() where T : struct
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<T>();

            var cache = GetComponentChunkIterator(typeIndex, out length, out componentIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new FixedArrayArray<T>(cache, length, m_SafetyManager.GetSafetyHandle(typeIndex, false));
#else
			return new FixedArrayArray<T>(cache, length);
#endif
        }

        public EntityArray GetEntityArray()
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<Entity>();
            var cache = GetComponentChunkIterator(typeIndex, out length, out componentIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new EntityArray(cache, length, m_SafetyManager.GetSafetyHandle(typeIndex, false));
#else
			return new EntityArray(cache, length);
#endif
        }
        public ComponentArray<T> GetComponentArray<T>() where T : Component
        {
            int length;
            int componentIndex;

            var cache = GetComponentChunkIterator(TypeManager.GetTypeIndex<T>(), out length, out componentIndex);
            return new ComponentArray<T>(cache, length, m_TypeManager);
        }

        public GameObjectArray GetGameObjectArray()
        {
            int length;
            int componentIndex;

            var cache = GetComponentChunkIterator(TypeManager.GetTypeIndex<Transform>(), out length,
                out componentIndex);
            return new GameObjectArray(cache, length, m_TypeManager);
        }

        public unsafe int CalculateLength()
        {
            int length;
            int componentIndex;
            GetComponentChunkIterator(TypeManager.GetTypeIndex<Entity>(), out length, out componentIndex);
            return length;
        }

        public TransformAccessArray GetTransformAccessArray()
        {
            int transformIdx = TypeManager.GetTypeIndex<Transform>();
            for (MatchingArchetypes* type = m_LastRegisteredListenerArchetype != null ? m_LastRegisteredListenerArchetype->next : m_GroupData->firstMatchingArchetype; type != null; type = type->next)
            {
                int idx = ChunkDataUtility.GetIndexInTypeArray(type->archetype, transformIdx);
                m_TypeManager.AddManagedObjectModificationListener(type->archetype, idx, this);
                m_TransformsDirty = true;
            }
            m_LastRegisteredListenerArchetype = m_GroupData->lastMatchingArchetype;

            if (m_TransformsDirty)
            {
                Profiling.Profiler.BeginSample("DirtyTransformAccessArrayUpdate");
                var trans = GetComponentArray<Transform>();
                if (!m_Transforms.IsCreated)
                    m_Transforms = new TransformAccessArray(trans.ToArray());
                else
                    m_Transforms.SetTransforms(trans.ToArray());
                Profiling.Profiler.EndSample();
            }

            m_TransformsDirty = false;
            return m_Transforms;
        }

		public Type[] Types
		{
			get
			{
				var types = new List<Type> ();
				for (int i = 0; i < m_GroupData->requiredComponentsCount; ++i)
                {
                    if (m_GroupData->requiredComponents[i].accessMode != ComponentType.AccessMode.Subtractive)
					    types.Add(TypeManager.GetType(m_GroupData->requiredComponents[i].typeIndex));
                }

				return types.ToArray ();
			}
		}


        private int GetComponentIndexForVariation<T>()
        {
            int componentType = TypeManager.GetTypeIndex<T>();
            int componentIndex = 0;
            while (componentIndex < m_GroupData->requiredComponentsCount && m_GroupData->requiredComponents[componentIndex].typeIndex != componentType)
                ++componentIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (componentIndex >= m_GroupData->requiredComponentsCount)
                throw new InvalidOperationException(string.Format("Trying to get a component group variation for {0} but the required component type was not declared in the ComponentGroup.", typeof(T)));
#endif
            return componentIndex;
        }

        public ComponentGroup GetVariation<SharedComponent1>(SharedComponent1 sharedComponent1)
            where SharedComponent1 : struct, ISharedComponentData
        {
            var variationComponentGroup = new ComponentGroup(m_GroupData, m_SafetyManager, m_TypeManager);

            int componetIndex1 = GetComponentIndexForVariation<SharedComponent1>();
            int filteredCount = 1;

            var filtered = (int*)UnsafeUtility.Malloc((filteredCount * 2 + 1) * sizeof(int), sizeof(int), Allocator.Temp); // TODO: does temp allocator make sense here?
            variationComponentGroup.m_filteredSharedComponents = filtered;


            filtered[0] = filteredCount;
            filtered[1] = componetIndex1;
            filtered[2] = m_TypeManager.GetSharedComponentDataManager().InsertSharedComponent(sharedComponent1);

            return variationComponentGroup;
        }

        public int GetVariationVersion<SharedComponent1>(SharedComponent1 sharedComponent1)
            where SharedComponent1 : struct, ISharedComponentData
        {
            return m_TypeManager.GetSharedComponentDataManager().GetSharedComponentVersion(sharedComponent1);
        }

        public ComponentGroup GetVariation<SharedComponent1, SharedComponent2>(SharedComponent1 sharedComponent1, SharedComponent2 sharedComponent2)
            where SharedComponent1 : struct, ISharedComponentData
            where SharedComponent2 : struct, ISharedComponentData
        {
            var variationComponentGroup = new ComponentGroup(m_GroupData, m_SafetyManager, m_TypeManager);

            int componetIndex1 = GetComponentIndexForVariation<SharedComponent1>();
            int componetIndex2 = GetComponentIndexForVariation<SharedComponent2>();
            int filteredCount = 2;

            var filtered = (int*)UnsafeUtility.Malloc((filteredCount * 2 + 1) * sizeof(int), sizeof(int), Allocator.Temp);
            variationComponentGroup.m_filteredSharedComponents = filtered;


            filtered[0] = filteredCount;
            filtered[1] = componetIndex1;
            filtered[2] = m_TypeManager.GetSharedComponentDataManager().InsertSharedComponent(sharedComponent1);
            filtered[3] = componetIndex2;
            filtered[4] = m_TypeManager.GetSharedComponentDataManager().InsertSharedComponent(sharedComponent2);

            return variationComponentGroup;
        }

        public void CompleteDependency()
        {
            m_SafetyManager.CompleteDependencies(m_GroupData->readerTypes, m_GroupData->readerTypesCount, m_GroupData->writerTypes, m_GroupData->writerTypesCount);
        }

        public JobHandle GetDependency()
        {
            return m_SafetyManager.GetDependency(m_GroupData->readerTypes, m_GroupData->readerTypesCount, m_GroupData->writerTypes, m_GroupData->writerTypesCount);
        }

        public void AddDependency(JobHandle job)
        {
            m_SafetyManager.AddDependency(m_GroupData->readerTypes, m_GroupData->readerTypesCount, m_GroupData->writerTypes, m_GroupData->writerTypesCount, job);
        }

        internal ArchetypeManager GetArchetypeManager()
        {
            return m_TypeManager;
        }
    }
}
