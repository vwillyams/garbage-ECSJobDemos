using System;
using System.Collections.Generic;
using UnityEngine.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
	public unsafe class EntityGroupManager : IDisposable
	{
		NativeMultiHashMap<uint, IntPtr>		m_GroupLookup;
		ChunkAllocator                          m_GroupDataChunkAllocator;
		EntityGroupData*                        m_LastGroupData;
        ComponentJobSafetyManager               m_JobSafetyManager;

        public EntityGroupManager(ComponentJobSafetyManager safetyManager)
		{
			m_GroupLookup = new NativeMultiHashMap<uint, IntPtr>(256, Allocator.Persistent);
            m_JobSafetyManager = safetyManager;
		}

        static bool CompareGroupComponents(int* type1, int typeCount1, int* type2, int typeCount2)
		{
			if (typeCount1 != typeCount2)
				return false;
			for (int i = 0; i < typeCount1; ++i)
			{
				if (type1[i] != type2[i])
					return false;
			}
			return true;
		}

		public EntityGroup CreateEntityGroup(TypeManager typeMan, int* requiredTypeIndices, int requiredCount)
		{
			uint hash = HashUtility.fletcher32((ushort*)requiredTypeIndices, requiredCount*2);
			NativeMultiHashMapIterator<uint> it;
			IntPtr grpPtr;
			EntityGroupData* grp;
			if (m_GroupLookup.TryGetFirstValue(hash, out grpPtr, out it))
			{
				do
				{
					grp = (EntityGroupData*)grpPtr;
					if (CompareGroupComponents(grp->m_RequiredComponents, grp->m_NumRequiredComponents, requiredTypeIndices, requiredCount))
						return new EntityGroup(grp, m_JobSafetyManager, typeMan);
				} while (m_GroupLookup.TryGetNextValue(out grpPtr, ref it));
			}
			grp = (EntityGroupData*)m_GroupDataChunkAllocator.Allocate(sizeof(EntityGroupData), 8);
			grp->m_PrevGroup = m_LastGroupData;
			m_LastGroupData = grp;
			grp->m_NumRequiredComponents = requiredCount;
			grp->m_RequiredComponents = (int*)m_GroupDataChunkAllocator.Allocate(sizeof(int)*requiredCount, 4);
			UnsafeUtility.MemCpy((IntPtr)grp->m_RequiredComponents, (IntPtr)requiredTypeIndices, sizeof(int)*requiredCount);
			grp->m_FirstMatchingArchetype = null;
			grp->m_LastMatchingArchetype = null;
			for (Archetype* type = typeMan.m_LastArchetype; type != null; type = type->prevArchetype)
				AddArchetypeIfMatching(type, grp);
			m_GroupLookup.Add(hash, (IntPtr)grp);
            return new EntityGroup(grp, m_JobSafetyManager, typeMan);
		}
		public void Dispose()
		{
            //@TODO: Need to wait for all job handles to be completed..

			m_GroupLookup.Dispose();
			m_GroupDataChunkAllocator.Dispose();
		}
		internal void OnArchetypeAdded(Archetype* type)
		{
			for (EntityGroupData* grp = m_LastGroupData; grp != null; grp = grp->m_PrevGroup)
				AddArchetypeIfMatching(type, grp);
		}
		void AddArchetypeIfMatching(Archetype* type, EntityGroupData* group)
		{
			if (group->m_NumRequiredComponents > type->typesCount)
				return;
			int typeI = 0;
			for (int i = 0; i < group->m_NumRequiredComponents; ++i, ++typeI)
			{
				while (type->types[typeI] < group->m_RequiredComponents[i] && typeI < type->typesCount)
					++typeI;
				if (typeI >= type->typesCount || type->types[typeI] != group->m_RequiredComponents[i])
					return;
			}
			MatchingArchetypes* match = (MatchingArchetypes*)m_GroupDataChunkAllocator.Allocate(sizeof(MatchingArchetypes), 8);
			match->archetype = type;
			match->archetypeSegments = (ComponentDataArchetypeSegment*)m_GroupDataChunkAllocator.Allocate(group->m_NumRequiredComponents*sizeof(ComponentDataArchetypeSegment), 8);
			match->next = null;
			if (group->m_LastMatchingArchetype != null)
				group->m_LastMatchingArchetype->next = match;
			else
				group->m_FirstMatchingArchetype = match;

			for (int component = 0; component < group->m_NumRequiredComponents; ++component)
			{
				match->archetypeSegments[component].archetype = type;
				match->archetypeSegments[component].nextSegment = null;
				if (group->m_LastMatchingArchetype != null)
					match->archetypeSegments[component].nextSegment = group->m_LastMatchingArchetype->archetypeSegments+component;
				int typeComponentIndex = 0;
				while (type->types[typeComponentIndex] != group->m_RequiredComponents[component])
					++typeComponentIndex;
				match->archetypeSegments[component].offset = type->offsets[typeComponentIndex];
				match->archetypeSegments[component].stride = type->strides[typeComponentIndex];
				match->archetypeSegments[component].typeIndex = typeComponentIndex;
			}

			group->m_LastMatchingArchetype = match;
		}
	}
	unsafe struct MatchingArchetypes
	{
		public Archetype* archetype;
		public ComponentDataArchetypeSegment* archetypeSegments;
		public MatchingArchetypes* next;
	}
	unsafe struct EntityGroupData
	{
		public int* m_RequiredComponents;
		public int m_NumRequiredComponents;
		public MatchingArchetypes* m_FirstMatchingArchetype;
		public MatchingArchetypes* m_LastMatchingArchetype;
		public EntityGroupData* m_PrevGroup;
	}

    //@TODO: Make safe when entity manager is destroyed.

	public unsafe class EntityGroup
	{
		EntityGroupData* m_GroupData;
        ComponentJobSafetyManager m_SafetyManager;
		TypeManager m_TypeManager;

        internal EntityGroup(EntityGroupData* groupData, ComponentJobSafetyManager safetyManager, TypeManager typeManager)
		{
			m_GroupData = groupData;
            m_SafetyManager = safetyManager;
			m_TypeManager = typeManager;
		}

        ComponentDataArchetypeSegment* GetSegmentData(int componentType, out int outLength, out int componentIndex)
        {
            componentIndex = 0;
            while (componentIndex < m_GroupData->m_NumRequiredComponents && m_GroupData->m_RequiredComponents[componentIndex] != componentType)
                ++componentIndex;
            if (componentIndex >= m_GroupData->m_NumRequiredComponents)
            {
                throw new InvalidOperationException(string.Format("Trying to get ComponentDataArray for {0} but the required component type was not declared in the EntityGroup.", RealTypeManager.GetType(componentType)));
            }
                
            // Update the archetype segments
            int length = 0;
			MatchingArchetypes* last = null;
            for (var match = m_GroupData->m_FirstMatchingArchetype; match != null; match = match->next)
			{
				if (match->archetype->entityCount > 0)
				{
	                length += match->archetype->entityCount;
					last = match;
				}
			}
            outLength = length;

			if (last == null)
				return null;
            return last->archetypeSegments + componentIndex;
        }


		public ComponentDataArray<T> GetComponentDataArray<T>(bool readOnly = false)where T : struct, IComponentData
		{
            int length;
			int componentIndex;
            int typeIndex = RealTypeManager.GetTypeIndex<T>();

            ComponentDataArchetypeSegment* segment = GetSegmentData(RealTypeManager.GetTypeIndex<T>(), out length, out componentIndex);
			#if ENABLE_NATIVE_ARRAY_CHECKS
			return new ComponentDataArray<T>(segment, length, m_SafetyManager.GetSafetyHandle(typeIndex), readOnly);
			#else
			return new ComponentDataArray<T>(segment, length);
			#endif
		}

        public ComponentDataFixedArray<T> GetComponentDataFixedArray<T>(ComponentType type, bool readOnly = false) where T : struct
        {
            int length;
			int componentIndex;
            ComponentDataArchetypeSegment* segment = GetSegmentData(type.typeIndex, out length, out componentIndex);
			#if ENABLE_NATIVE_ARRAY_CHECKS
			return new ComponentDataFixedArray<T>(segment, length, 64, m_SafetyManager.GetSafetyHandle(type.typeIndex), readOnly);
			#else
			return new ComponentDataFixedArray<T>(segment, length, 64);
			#endif
        }

		public EntityArray GetEntityArray()
		{
            int length;
			int componentIndex;
            int typeIndex = RealTypeManager.GetTypeIndex<Entity>();
            ComponentDataArchetypeSegment* segment = GetSegmentData(typeIndex, out length, out componentIndex);
			#if ENABLE_NATIVE_ARRAY_CHECKS
			return new EntityArray(segment, length, m_SafetyManager.GetSafetyHandle(typeIndex));
			#else
			return new EntityArray(segment, length);
			#endif
		}
		public ComponentArray<T> GetComponentArray<T>()where T : Component
		{
            int length;
			int componentIndex;

            ComponentDataArchetypeSegment* segment = GetSegmentData(RealTypeManager.GetTypeIndex<T>(), out length, out componentIndex);
			return new ComponentArray<T>(segment, length, m_TypeManager);
		}
		public void UpdateTransformAccessArray(TransformAccessArray transforms)
		{
			var trans = GetComponentArray<Transform>();
			while (transforms.Length > 0)
				transforms.RemoveAtSwapBack(transforms.Length-1);
			for (int i = 0; i < trans.Length; ++i)
				transforms.Add(trans[i]);
		}

		public Type[] Types
		{
			get
			{
				var types = new List<Type> ();
				for (int i = 0; i < m_GroupData->m_NumRequiredComponents; ++i)
					types.Add(RealTypeManager.GetType(m_GroupData->m_RequiredComponents[i]));

				return types.ToArray ();
			}
		}
	}
}
