using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
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

            //@TODO: Allow creation of ComponentGroup to understand read only types...
            int* types = (int*)m_GroupDataChunkAllocator.Allocate(sizeof(int) * requiredCount, 4);
            grp->readerTypesCount = 0;
            grp->writerTypesCount = 0;

            for (int i = 0; i != requiredCount;i++)
            {
                types[i] = requiredTypes[i].typeIndex;
                grp->writerTypesCount++;
            }
            grp->writerTypes = types;
            grp->readerTypes = null;

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
            if (group->requiredComponentsCount > archetype->typesCount)
                return;
            int typeI = 0;
            for (int i = 0; i < group->requiredComponentsCount; ++i, ++typeI)
            {
                while (archetype->types[typeI].typeIndex < group->requiredComponents[i].typeIndex && typeI < archetype->typesCount)
                    ++typeI;
                
                if (typeI >= archetype->typesCount)
                    return;

                // Type mismatch
                if (archetype->types[typeI].typeIndex != group->requiredComponents[i].typeIndex)
                    return;

                // We are looking for a specific shared type index and it doesn't match
                if (group->requiredComponents[i].sharedComponentIndex != -1 && archetype->types[typeI].sharedComponentIndex != group->requiredComponents[i].sharedComponentIndex)
                    return;
            }
            MatchingArchetypes* match = (MatchingArchetypes*)m_GroupDataChunkAllocator.Allocate(sizeof(MatchingArchetypes), 8);
            match->archetype = archetype;
            match->archetypeSegments = (ComponentDataArchetypeSegment*)m_GroupDataChunkAllocator.Allocate(group->requiredComponentsCount * sizeof(ComponentDataArchetypeSegment), 8);
            match->next = null;
            if (group->lastMatchingArchetype != null)
                group->lastMatchingArchetype->next = match;
            else
                group->firstMatchingArchetype = match;

            for (int component = 0; component < group->requiredComponentsCount; ++component)
            {
                match->archetypeSegments[component].archetype = archetype;
                match->archetypeSegments[component].nextSegment = null;
                if (group->lastMatchingArchetype != null)
                    match->archetypeSegments[component].nextSegment = group->lastMatchingArchetype->archetypeSegments + component;

                int typeComponentIndex = ChunkDataUtility.GetIndexInTypeArray(archetype, group->requiredComponents[component].typeIndex);
                Assertions.Assert.AreNotEqual(-1, typeComponentIndex);

                match->archetypeSegments[component].typeIndexInArchetype = typeComponentIndex;
            }

            group->lastMatchingArchetype = match;
        }
    }
    unsafe struct MatchingArchetypes
    {
        public Archetype*                       archetype;
        public ComponentDataArchetypeSegment*   archetypeSegments;
        public MatchingArchetypes*              next;
    }
    unsafe struct EntityGroupData
    {
        public int*                 readerTypes;
        public int                  readerTypesCount;

        public int*                 writerTypes;
        public int                  writerTypesCount;

        public ComponentType*       requiredComponents;
        public int                  requiredComponentsCount;
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

        internal ComponentGroup(EntityGroupData* groupData, ComponentJobSafetyManager safetyManager, ArchetypeManager typeManager)
        {
            m_GroupData = groupData;
            m_SafetyManager = safetyManager;
            m_TypeManager = typeManager;
            m_TransformsDirty = true;
            m_LastRegisteredListenerArchetype = null;
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
        }
        public void OnManagedObjectModified()
        {
            m_TransformsDirty = true;
        }

        bool IsReadOnly(int componentIndex)
        {
            return m_GroupData->requiredComponents[componentIndex].readOnly != 0;
        }

        internal static void AddReaderWriter(ComponentType type, List<int> reading, List<int> writing)
        {
            if (!type.RequiresJobDependency)
                return;
                    
            if (type.readOnly != 0)
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


        ComponentDataArrayCache GetComponentDataArrayCache(int componentType, out int outLength, out int componentIndex)
        {
            componentIndex = 0;
            while (componentIndex < m_GroupData->requiredComponentsCount && m_GroupData->requiredComponents[componentIndex].typeIndex != componentType)
                ++componentIndex;
            if (componentIndex >= m_GroupData->requiredComponentsCount)
            {
                throw new InvalidOperationException(string.Format("Trying to get ComponentDataArray for {0} but the required component type was not declared in the EntityGroup.", TypeManager.GetType(componentType)));
            }

            // Update the archetype segments
            int length = 0;
            MatchingArchetypes* last = null;
            for (var match = m_GroupData->firstMatchingArchetype; match != null; match = match->next)
            {
                if (match->archetype->entityCount > 0)
                {
                    length += match->archetype->entityCount;
                    last = match;
                }
            }
            outLength = length;

            if (last == null)
                return new ComponentDataArrayCache(null, 0);
            return new ComponentDataArrayCache(last->archetypeSegments + componentIndex, length);
        }

        public ComponentDataArray<T> GetComponentDataArray<T>() where T : struct, IComponentData
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<T>();

            var cache = GetComponentDataArrayCache(TypeManager.GetTypeIndex<T>(), out length, out componentIndex);
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

            var cache = GetComponentDataArrayCache(typeIndex, out length, out componentIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new FixedArrayArray<T>(cache, length, m_SafetyManager.GetSafetyHandle(typeIndex, false));
#else
			return new ComponentDataFixedArray<T>(cache, length);
#endif
        }

        public EntityArray GetEntityArray()
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<Entity>();
            var cache = GetComponentDataArrayCache(typeIndex, out length, out componentIndex);
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

            var cache = GetComponentDataArrayCache(TypeManager.GetTypeIndex<T>(), out length, out componentIndex);
            return new ComponentArray<T>(cache, length, m_TypeManager);
        }

        public int Length
        {
            get
            {
                int length;
                int componentIndex;
                GetComponentDataArrayCache(TypeManager.GetTypeIndex<Entity>(), out length, out componentIndex);
                return length;
            }
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
                var trans = GetComponentArray<Transform>();
                if (!m_Transforms.IsCreated)
                    m_Transforms = new TransformAccessArray(trans.ToArray());
                else
                    m_Transforms.SetTransforms(trans.ToArray());
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
					types.Add(TypeManager.GetType(m_GroupData->requiredComponents[i].typeIndex));

				return types.ToArray ();
			}
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
    }
}
