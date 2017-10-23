using System;
using System.Collections.Generic;
using UnityEngine.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
    internal unsafe class EntityGroupManager : IDisposable
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
                    if (ComponentType.CompareArray(grp->requiredComponents, grp->numRequiredComponents, requiredTypes, requiredCount))
                        return new ComponentGroup(grp, m_JobSafetyManager, typeMan, trans);
                }
                while (m_GroupLookup.TryGetNextValue(out grpPtr, ref it));
            }

            m_JobSafetyManager.CompleteAllJobsAndInvalidateArrays();

            grp = (EntityGroupData*)m_GroupDataChunkAllocator.Allocate(sizeof(EntityGroupData), 8);
            grp->prevGroup = m_LastGroupData;
            m_LastGroupData = grp;
            grp->numRequiredComponents = requiredCount;
            grp->requiredComponents = (ComponentType*)m_GroupDataChunkAllocator.Construct(sizeof(ComponentType) * requiredCount, 4, requiredTypes);

            grp->firstMatchingArchetype = null;
            grp->lastMatchingArchetype = null;
            for (Archetype* type = typeMan.m_LastArchetype; type != null; type = type->prevArchetype)
                AddArchetypeIfMatching(type, grp);
            m_GroupLookup.Add(hash, (IntPtr)grp);
            return new ComponentGroup(grp, m_JobSafetyManager, typeMan, trans);
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
            if (group->numRequiredComponents > archetype->typesCount)
                return;
            int typeI = 0;
            for (int i = 0; i < group->numRequiredComponents; ++i, ++typeI)
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
            match->archetypeSegments = (ComponentDataArchetypeSegment*)m_GroupDataChunkAllocator.Allocate(group->numRequiredComponents * sizeof(ComponentDataArchetypeSegment), 8);
            match->next = null;
            if (group->lastMatchingArchetype != null)
                group->lastMatchingArchetype->next = match;
            else
                group->firstMatchingArchetype = match;

            for (int component = 0; component < group->numRequiredComponents; ++component)
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
        public ComponentType*       requiredComponents;
        public int                  numRequiredComponents;
        public MatchingArchetypes*  firstMatchingArchetype;
        public MatchingArchetypes*  lastMatchingArchetype;
        public EntityGroupData*     prevGroup;
    }

    //@TODO: Make safe when entity manager is destroyed.
    //@TODO: This needs to become a struct

    public unsafe class ComponentGroup : IDisposable, IManagedObjectModificationListener
    {
        EntityGroupData*                m_GroupData;
        ComponentJobSafetyManager       m_SafetyManager;
        ArchetypeManager                     m_TypeManager;
        TransformAccessArray            m_Transforms;
        bool                            m_TransformsDirty;
        MatchingArchetypes*             m_LastRegisteredListenerArchetype;

        internal ComponentGroup(EntityGroupData* groupData, ComponentJobSafetyManager safetyManager, ArchetypeManager typeManager, TransformAccessArray trans)
        {
            m_GroupData = groupData;
            m_SafetyManager = safetyManager;
            m_TypeManager = typeManager;
            m_Transforms = trans;
            m_TransformsDirty = true;

            if (m_Transforms.IsCreated)
            {
                var transformType = TypeManager.GetTypeIndex<Transform>();
                for (MatchingArchetypes* type = m_GroupData->firstMatchingArchetype; type != null; type = type->next)
                {
                    int idx = ChunkDataUtility.GetIndexInTypeArray(type->archetype, transformType);
                    m_TypeManager.AddManagedObjectModificationListener(type->archetype, idx, this);
                }
            }
            m_LastRegisteredListenerArchetype = m_GroupData->lastMatchingArchetype;
        }

        public void Dispose()
        {
            if (m_Transforms.IsCreated)
            {
                if (m_LastRegisteredListenerArchetype != null)
                {
                    var transformType = TypeManager.GetTypeIndex<Transform>();
                    for (MatchingArchetypes* type = m_GroupData->firstMatchingArchetype; type != m_LastRegisteredListenerArchetype->next; type = type->next)
                    {
                        int idx = ChunkDataUtility.GetIndexInTypeArray(type->archetype, transformType);
                        m_TypeManager.RemoveManagedObjectModificationListener(type->archetype, idx, this);
                    }
                }
                m_Transforms.Dispose();
            }
        }
        public void OnManagedObjectModified()
        {
            m_TransformsDirty = true;
        }

        ComponentDataArrayCache GetComponentDataArrayCache(int componentType, out int outLength, out int componentIndex)
        {
            componentIndex = 0;
            while (componentIndex < m_GroupData->numRequiredComponents && m_GroupData->requiredComponents[componentIndex].typeIndex != componentType)
                ++componentIndex;
            if (componentIndex >= m_GroupData->numRequiredComponents)
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


        public ComponentDataArray<T> GetComponentDataArray<T>(bool readOnly = false) where T : struct, IComponentData
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<T>();

            var cache = GetComponentDataArrayCache(TypeManager.GetTypeIndex<T>(), out length, out componentIndex);
#if ENABLE_NATIVE_ARRAY_CHECKS
            return new ComponentDataArray<T>(cache, length, m_SafetyManager.GetSafetyHandle(typeIndex), readOnly);
#else
			return new ComponentDataArray<T>(cache, length);
#endif
        }

        public ComponentDataFixedArray<T> GetComponentDataFixedArray<T>(ComponentType type, bool readOnly = false) where T : struct
        {
            int length;
            int componentIndex;
            var cache = GetComponentDataArrayCache(type.typeIndex, out length, out componentIndex);
#if ENABLE_NATIVE_ARRAY_CHECKS
            return new ComponentDataFixedArray<T>(cache, length, 64, m_SafetyManager.GetSafetyHandle(type.typeIndex), readOnly);
#else
			return new ComponentDataFixedArray<T>(cache, length, 64);
#endif
        }

        public EntityArray GetEntityArray()
        {
            int length;
            int componentIndex;
            int typeIndex = TypeManager.GetTypeIndex<Entity>();
            var cache = GetComponentDataArrayCache(typeIndex, out length, out componentIndex);
#if ENABLE_NATIVE_ARRAY_CHECKS
            return new EntityArray(cache, length, m_SafetyManager.GetSafetyHandle(typeIndex));
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

        internal void UpdateTransformAccessArray()
        {
            if (!m_Transforms.IsCreated)
                return;
            int transformIdx = TypeManager.GetTypeIndex<Transform>();
            for (MatchingArchetypes* type = m_LastRegisteredListenerArchetype != null ? m_LastRegisteredListenerArchetype->next : m_GroupData->firstMatchingArchetype; type != null; type = type->next)
            {
                int idx = ChunkDataUtility.GetIndexInTypeArray(type->archetype, transformIdx);
                m_TypeManager.AddManagedObjectModificationListener(type->archetype, idx, this);
                m_TransformsDirty = true;
            }
            m_LastRegisteredListenerArchetype = m_GroupData->lastMatchingArchetype;
            if (!m_TransformsDirty)
                return;
            m_TransformsDirty = false;
            var trans = GetComponentArray<Transform>();

		    m_Transforms.SetTransforms(trans.ToArray());
        }

		public Type[] Types
		{
			get
			{
				var types = new List<Type> ();
				for (int i = 0; i < m_GroupData->numRequiredComponents; ++i)
					types.Add(TypeManager.GetType(m_GroupData->requiredComponents[i].typeIndex));

				return types.ToArray ();
			}
		}
	}
}
