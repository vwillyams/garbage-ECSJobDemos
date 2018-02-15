using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


using Transform = UnityEngine.Transform;
using Component = UnityEngine.Component;
using TransformAccessArray = UnityEngine.Jobs.TransformAccessArray;

namespace Unity.ECS
{
    unsafe class EntityGroupManager : IDisposable
    {
        NativeMultiHashMap<uint, IntPtr>    m_GroupLookup;
        ChunkAllocator                      m_GroupDataChunkAllocator;
        EntityGroupData*                    m_LastGroupData;
        readonly ComponentJobSafetyManager  m_JobSafetyManager;

        public EntityGroupManager(ComponentJobSafetyManager safetyManager)
        {
            m_GroupLookup = new NativeMultiHashMap<uint, IntPtr>(256, Allocator.Persistent);
            m_JobSafetyManager = safetyManager;
        }

        private EntityGroupData* GetCachedGroupData(uint hash, ComponentType* requiredTypes,
            int requiredCount)
        {
            NativeMultiHashMapIterator<uint> it;
            IntPtr grpPtr;
            if (!m_GroupLookup.TryGetFirstValue(hash, out grpPtr, out it))
                return null;
            do
            {
                var grp = (EntityGroupData*) grpPtr;
                if (ComponentType.CompareArray(grp->RequiredComponents, grp->RequiredComponentsCount, requiredTypes,
                    requiredCount))
                    return grp;
            } while (m_GroupLookup.TryGetNextValue(out grpPtr, ref it));

            return null;
        }
        public ComponentGroup CreateEntityGroupIfCached(ArchetypeManager typeMan, ComponentType* requiredTypes,
            int requiredCount)
        {
            var hash = HashUtility.Fletcher32((ushort*) requiredTypes,
                requiredCount * sizeof(ComponentType) / sizeof(short));
            EntityGroupData* grp = GetCachedGroupData(hash, requiredTypes, requiredCount);
            if (grp != null)
                return new ComponentGroup(grp, m_JobSafetyManager, typeMan);
            return null;
        }

        public ComponentGroup CreateEntityGroup(ArchetypeManager typeMan, ComponentType* requiredTypes, int requiredCount)
        {
            var hash = HashUtility.Fletcher32((ushort*)requiredTypes, requiredCount * sizeof(ComponentType) / sizeof(short));
            EntityGroupData* grp = GetCachedGroupData(hash, requiredTypes, requiredCount);
            if (grp != null)
                return new ComponentGroup(grp, m_JobSafetyManager, typeMan);

            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            grp = (EntityGroupData*)m_GroupDataChunkAllocator.Allocate(sizeof(EntityGroupData), 8);
            grp->PrevGroup = m_LastGroupData;
            m_LastGroupData = grp;
            grp->RequiredComponentsCount = requiredCount;
            grp->RequiredComponents = (ComponentType*)m_GroupDataChunkAllocator.Construct(sizeof(ComponentType) * requiredCount, 4, requiredTypes);

            grp->ReaderTypesCount = 0;
            grp->WriterTypesCount = 0;

            grp->SubtractiveComponentsCount = 0;

            for (var i = 0; i != requiredCount;i++)
            {
                if (!requiredTypes[i].RequiresJobDependency && requiredTypes[i].AccessModeType != ComponentType.AccessMode.Subtractive)
                    continue;
                switch (requiredTypes[i].AccessModeType)
                {
                    case ComponentType.AccessMode.Subtractive:
                        grp->SubtractiveComponentsCount++;
                        break;
                    case ComponentType.AccessMode.ReadOnly:
                        grp->ReaderTypesCount++;
                        break;
                    default:
                        grp->WriterTypesCount++;
                        break;
                }
            }
            grp->ReaderTypes = (int*)m_GroupDataChunkAllocator.Allocate(sizeof(int) * grp->ReaderTypesCount, 4);
            grp->WriterTypes = (int*)m_GroupDataChunkAllocator.Allocate(sizeof(int) * grp->WriterTypesCount, 4);

            var curReader = 0;
            var curWriter = 0;
            for (var i = 0; i != requiredCount;i++)
            {
                if (!requiredTypes[i].RequiresJobDependency && requiredTypes[i].AccessModeType != ComponentType.AccessMode.Subtractive)
                    continue;
                switch (requiredTypes[i].AccessModeType)
                {
                    case ComponentType.AccessMode.Subtractive:
                        break;
                    case ComponentType.AccessMode.ReadOnly:
                        grp->ReaderTypes[curReader++] = requiredTypes[i].TypeIndex;
                        break;
                    default:
                        grp->WriterTypes[curWriter++] = requiredTypes[i].TypeIndex;
                        break;
                }
            }

            grp->RequiredComponents = (ComponentType*)m_GroupDataChunkAllocator.Construct(sizeof(ComponentType) * requiredCount, 4, requiredTypes);

            grp->FirstMatchingArchetype = null;
            grp->LastMatchingArchetype = null;
            for (var type = typeMan.m_LastArchetype; type != null; type = type->PrevArchetype)
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
            for (var grp = m_LastGroupData; grp != null; grp = grp->PrevGroup)
                AddArchetypeIfMatching(type, grp);
        }

        void AddArchetypeIfMatching(Archetype* archetype, EntityGroupData* group)
        {
            if (group->RequiredComponentsCount - group->SubtractiveComponentsCount > archetype->TypesCount)
                return;
            var typeI = 0;
            var prevTypeI = 0;
            for (var i = 0; i < group->RequiredComponentsCount; ++i, ++typeI)
            {
                while (archetype->Types[typeI].TypeIndex < group->RequiredComponents[i].TypeIndex && typeI < archetype->TypesCount)
                    ++typeI;

                var hasComponent = !(typeI >= archetype->TypesCount);

                // Type mismatch
                if (hasComponent && archetype->Types[typeI].TypeIndex != group->RequiredComponents[i].TypeIndex)
                    hasComponent = false;

                if (hasComponent && group->RequiredComponents[i].AccessModeType == ComponentType.AccessMode.Subtractive)
                    return;
                if (!hasComponent && group->RequiredComponents[i].AccessModeType != ComponentType.AccessMode.Subtractive)
                    return;
                if (hasComponent)
                    prevTypeI = typeI;
                else
                    typeI = prevTypeI;
            }
            var match = (MatchingArchetypes*)m_GroupDataChunkAllocator.Allocate(MatchingArchetypes.GetAllocationSize(group->RequiredComponentsCount), 8);
            match->Archetype = archetype;
            var typeIndexInArchetypeArray = match->TypeIndexInArchetypeArray;

            if (group->LastMatchingArchetype == null)
                group->LastMatchingArchetype = match;

            match->Next = group->FirstMatchingArchetype;
            group->FirstMatchingArchetype = match;

            for (var component = 0; component < group->RequiredComponentsCount; ++component)
            {
                var typeComponentIndex = -1;
                if (group->RequiredComponents[component].AccessModeType != ComponentType.AccessMode.Subtractive)
                {
                    typeComponentIndex = ChunkDataUtility.GetIndexInTypeArray(archetype, group->RequiredComponents[component].TypeIndex);
                    UnityEngine.Assertions.Assert.AreNotEqual(-1, typeComponentIndex);
                }

                typeIndexInArchetypeArray[component] = typeComponentIndex;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct MatchingArchetypes
    {
        public Archetype*                       Archetype;
        public MatchingArchetypes*              Next;
        public fixed int                        TypeIndexInArchetypeArray[1];

        public static int GetAllocationSize(int requiredComponentsCount)
        {
            return sizeof(MatchingArchetypes) + sizeof(int) * (requiredComponentsCount - 1);
        }
    }

    unsafe struct EntityGroupData
    {
        public int*                 ReaderTypes;
        public int                  ReaderTypesCount;

        public int*                 WriterTypes;
        public int                  WriterTypesCount;

        public ComponentType*       RequiredComponents;
        public int                  RequiredComponentsCount;
        public int                  SubtractiveComponentsCount;
        public MatchingArchetypes*  FirstMatchingArchetype;
        public MatchingArchetypes*  LastMatchingArchetype;
        public EntityGroupData*     PrevGroup;
    }

    public unsafe class ComponentGroup : IDisposable, IManagedObjectModificationListener
    {
        readonly EntityGroupData*             m_GroupData;
        readonly ComponentJobSafetyManager    m_SafetyManager;
        readonly ArchetypeManager             m_TypeManager;
        MatchingArchetypes*                   m_LastRegisteredListenerArchetype;

        TransformAccessArray                  m_Transforms;
        bool                                  m_TransformsDirty;
        int*                                  m_FilteredSharedComponents;

        internal ComponentGroup(EntityGroupData* groupData, ComponentJobSafetyManager safetyManager, ArchetypeManager typeManager)
        {
            m_GroupData = groupData;
            m_SafetyManager = safetyManager;
            m_TypeManager = typeManager;
            m_TransformsDirty = true;
            m_LastRegisteredListenerArchetype = null;
            m_FilteredSharedComponents = null;
        }

        public void Dispose()
        {
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
        AtomicSafetyHandle GetSafetyHandle(int indexInComponentGroup)
        {
            var type = m_GroupData->RequiredComponents + indexInComponentGroup;
            var isReadOnly = type->AccessModeType == ComponentType.AccessMode.ReadOnly;
            return m_SafetyManager.GetSafetyHandle(type->TypeIndex, isReadOnly);
        }
#endif

        internal static void AddReaderWriter(ComponentType type, List<int> reading, List<int> writing)
        {
            if (!type.RequiresJobDependency)
                return;

            if (type.AccessModeType == ComponentType.AccessMode.ReadOnly)
            {
                if (reading.Contains(type.TypeIndex))
                    return;
                if (writing.Contains(type.TypeIndex))
                    return;

                reading.Add(type.TypeIndex);
            }
            else
            {
                if (reading.Contains(type.TypeIndex))
                    reading.Remove(type.TypeIndex);
                if (writing.Contains(type.TypeIndex))
                    return;
                writing.Add(type.TypeIndex);
            }
        }

        internal static void ExtractJobDependencyTypes(ComponentGroup[] groups, List<int> reading, List<int> writing)
        {
            foreach (var group in groups)
            {
                for (var i = 0;i != group.m_GroupData->RequiredComponentsCount;i++)
                {
                    var type = group.m_GroupData->RequiredComponents[i];
                    AddReaderWriter(type, reading, writing);
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

        public ComponentDataArray<T> GetComponentDataArray<T>(Type componentType) where T : struct, IComponentData
        {
            int length;
            ComponentChunkIterator iterator;
            GetComponentChunkIterator(out length, out iterator);
            var indexInComponentGroup = GetIndexInComponentGroup(TypeManager.GetTypeIndex(componentType));

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
            var variationComponentGroup = new ComponentGroup(m_GroupData, m_SafetyManager, m_TypeManager);

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
            var variationComponentGroup = new ComponentGroup(m_GroupData, m_SafetyManager, m_TypeManager);

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
