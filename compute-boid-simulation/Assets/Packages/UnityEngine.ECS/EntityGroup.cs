using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
	public interface IEntityGroupChange
	{
		void OnAddElements (int numberOfEntitiesAddedToGroup);
		void OnRemoveSwapBack (int indexOfEntityToBeRemoved);
	}

	public class EntityGroup
	{
		#if !ECS_ENTITY_CLASS
		//@TODO: Renaming
		internal class RegisteredTuple
		{
			public EntityGroup 	tupleSystem;
			public int 			tupleSystemIndex;

			public RegisteredTuple(EntityGroup tupleSystem, int tupleSystemIndex)
			{
				this.tupleSystemIndex = tupleSystemIndex;
				this.tupleSystem = tupleSystem;
			}
		}
		#endif

		interface IGenericComponentList
		{
			void AddComponent (Component com);
			void RemoveAtSwapBackComponent (int index);
			int GetIndex (Component com);
		}

		class GenericComponentList<T> : List<T>, IGenericComponentList where T : Component
		{
			public void AddComponent (Component com)
			{
				Add ((T)com);
			}
			public void RemoveAtSwapBackComponent (int index)
			{
				this.RemoveAtSwapBack (index);
			}
			public int GetIndex (Component com)
			{
				for (int i = 0; i != Count; i++)
				{
					if (com == this [i])
						return 1;
				}
				return -1;
			}
		}


		IEntityGroupChange 				m_ChangeEvent;

		// Transforms
		TransformAccessArray	 		m_Transforms;

		// ComponentData
		int[]                    		m_ComponentDataTypes;
		#if ECS_ENTITY_CLASS
		NativeList<ComponentDataIndexSegment>[]       		m_ComponentDataIndexSegments;
		#else
		NativeList<int>[]       		m_ComponentDataIndices;
		#endif
		ScriptBehaviourManager[] 		m_ComponentDataManagers;
		EntityManager 			 		m_EntityManager;

		// ComponentType
		Type[]                   		m_ComponentTypes;
		IGenericComponentList[] 		m_ComponentLists;

		NativeHashMap<int, int>	 		m_EntityToTupleIndex;
		NativeList<Entity>		 		m_TupleToEntityIndex;

		public EntityGroup (EntityManager entityManager, params Type[] requiredComponents)
		{
			Type[] componentDataTypes;
			Type[] componentTypes;
			SplitComponents (requiredComponents, out componentDataTypes, out componentTypes);

			var componentDataManagers = new ScriptBehaviourManager[componentDataTypes.Length];
			for (int i = 0; i != componentDataManagers.Length; i++)
			{
				var managerType = typeof(ComponentDataManager<>).MakeGenericType (componentDataTypes [i]);
				componentDataManagers[i] = DependencyManager.GetBehaviourManager (managerType);
			}

			Initialize (entityManager, componentDataTypes, componentDataManagers, componentTypes, new TransformAccessArray());
		}

		public EntityGroup (EntityManager entityManager, Type[] componentDataTypes, ScriptBehaviourManager[] componentDataManagers, Type[] componentTypes, TransformAccessArray transforms)
		{
			Initialize (entityManager, componentDataTypes, componentDataManagers, componentTypes, transforms);
		}


		void Initialize (EntityManager entityManager, Type[] componentDataTypes, ScriptBehaviourManager[] componentDataManagers, Type[] componentTypes, TransformAccessArray transforms)
		{
			int capacity = 0;

			m_EntityManager = entityManager;

			// transforms
			m_Transforms = transforms;

			// entity
			m_EntityToTupleIndex = new NativeHashMap<int, int> (capacity, Allocator.Persistent);
			m_TupleToEntityIndex = new NativeList<Entity>(capacity, Allocator.Persistent);

			// components
			m_ComponentLists = new IGenericComponentList[componentTypes.Length];
			m_ComponentTypes = componentTypes;
			for (int i = 0; i != componentTypes.Length; i++)
			{
				var componentType = componentTypes[i];

				var listType = typeof(GenericComponentList<>).MakeGenericType (new Type[] { componentType });
				m_ComponentLists [i] = (IGenericComponentList)Activator.CreateInstance (listType);
			}

			// Component data
			#if ECS_ENTITY_CLASS
			m_ComponentDataIndexSegments = new NativeList<ComponentDataIndexSegment>[componentDataTypes.Length];
			#else
			m_ComponentDataIndices = new NativeList<int>[componentDataTypes.Length];
			#endif
			m_ComponentDataManagers = componentDataManagers;
			m_ComponentDataTypes = new int[componentDataTypes.Length];
			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{
				#if ECS_ENTITY_CLASS
				m_ComponentDataIndexSegments[i] = new NativeList<ComponentDataIndexSegment>(0, Allocator.Persistent);
				#else
				m_ComponentDataIndices[i] = new NativeList<int>(0, Allocator.Persistent);
				#endif
				m_ComponentDataTypes[i] = entityManager.GetTypeIndex(componentDataTypes[i]);
			}

			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{
				m_EntityManager.RegisterTuple (m_ComponentDataTypes[i], this, i);
			}
		}

		static void SplitComponents(Type[] anyComponents, out Type[] outComponentDataTypes, out Type[] outComponentTypes)
		{
			var componentDataTypes = new List<Type> (anyComponents.Length);
			var componentTypes = new List<Type> (anyComponents.Length);

			foreach (var com in anyComponents)
			{
				if (com.IsSubclassOf (typeof(Component)))
					componentTypes.Add (com);
				else if (com.IsValueType && typeof(IComponentData).IsAssignableFrom (com))
					componentDataTypes.Add (com);
				else
					throw new System.ArgumentException (com + " is not a valid Component or IComponentData");					
			}

			outComponentDataTypes = componentDataTypes.ToArray ();
			outComponentTypes = componentTypes.ToArray ();
		}

		public EntityArray GetEntityArray()
		{
			EntityArray array;
			#if ECS_ENTITY_CLASS
			m_TupleToEntityIndex.Clear();
			for (int i = 0; i < m_MatchingClasses.Count; ++i)
			{
				for (int j = 0; j < m_MatchingClasses[i].entities.Length; ++j)
					m_TupleToEntityIndex.Add(m_MatchingClasses[i].entities[j]);
			}
			#endif
			array.m_Array = m_TupleToEntityIndex;
			return array;
		}

		public ComponentDataArray<T> GetComponentDataArray<T>(bool readOnly = false)where T : struct, IComponentData
		{
			int componentTypeIndex = m_EntityManager.GetTypeIndex<T> ();
			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{
				if (m_ComponentDataTypes[i] == componentTypeIndex)
					return GetComponentDataArray<T> (i, readOnly);
			}

			throw new System.ArgumentException (typeof(T) + " is not part of the EntityGroup");
		}

		#if ECS_ENTITY_CLASS
		internal unsafe ComponentDataArray<T> GetComponentDataArray<T>(int index, bool readOnly) where T : struct, IComponentData
		#else
		internal ComponentDataArray<T> GetComponentDataArray<T>(int index, bool readOnly) where T : struct, IComponentData
		#endif
		{
			var manager = m_ComponentDataManagers[index] as ComponentDataManager<T>;

			#if ECS_ENTITY_CLASS
			// FIXME: only if dirty
			if (m_Transforms.IsCreated)
			{
				while (m_Transforms.Length > 0)
					m_Transforms.RemoveAtSwapBack(0);
				for (int i = 0; i < m_MatchingClasses.Count; ++i)
				{
					for (int j = 0; j < m_MatchingClasses[i].entities.Length; ++j)
					{
						var fullGameObject = UnityEditor.EditorUtility.InstanceIDToObject (m_MatchingClasses[i].entities[j].index) as GameObject;
						m_Transforms.Add(fullGameObject.transform);
					}
				}
			}
			m_ComponentDataIndexSegments[index].Clear();
			int curLen = 0;
			for (int i = 0; i < m_MatchingClasses.Count; ++i)
			{
				int typeOffset = 0;
				for (int j = 0; j < m_MatchingClasses[i].componentTypes.Length; ++j)
				{
					if (m_MatchingClasses[i].componentTypes[j] == m_ComponentDataTypes[index])
						typeOffset = j;
				}
				ComponentDataIndexSegment segment;
				segment.indices = (int*)m_MatchingClasses[i].componentDataIndices.UnsafePtr;
				segment.beginIndex = curLen;
				segment.endIndex = curLen + m_MatchingClasses[i].entities.Length;
				segment.offset = typeOffset;
				segment.stride = m_MatchingClasses[i].componentTypes.Length;
				curLen += m_MatchingClasses[i].entities.Length;

				m_ComponentDataIndexSegments[index].Add(segment);
			}
			#endif

			#if ECS_ENTITY_CLASS
			var container = new ComponentDataArray<T> (manager.m_Data, m_ComponentDataIndexSegments[index], readOnly);
			#else
			var container = new ComponentDataArray<T> (manager.m_Data, m_ComponentDataIndices[index], readOnly);
			#endif
			return container;
		}

		internal ComponentArray<T> GetComponentArray<T>(int index) where T : Component
		{
			ComponentArray<T> array;
			array.m_List = (List<T>)m_ComponentLists[index];
			return array;
		}

		#if !ECS_ENTITY_CLASS
		bool IsTupleSupported(GameObject go, Entity lightGameObject)
		{
			foreach (var componentType in m_ComponentTypes)
			{
				var component = go.GetComponent (componentType);
				if (component == null)
					return false;
			}

			foreach (var componentType in m_ComponentDataTypes)
			{
				if (m_EntityManager.GetComponentIndex (lightGameObject, componentType) == -1)
					return false;
			}

			if (m_Transforms.IsCreated && go == null)
				return false;

			return true;
		}

		public bool IsComponentDataTypesSupported(NativeArray<int> types)
		{
			if (m_Transforms.IsCreated)
				return false;
			if (m_ComponentTypes.Length != 0)
				return false;

			foreach (var componentType in m_ComponentDataTypes)
			{
				if (types.IndexOf(componentType) == -1)
					return false;
			}

			return true;
		}

		public void RemoveSwapBackComponentData(Entity entity)
		{
			int tupleIndex;
			if (!m_EntityToTupleIndex.TryGetValue (entity.index, out tupleIndex))
				return;

			if (tupleIndex == -1)
				return;

			RemoveSwapBackTupleIndex(tupleIndex);
		}

		public void RemoveSwapBackComponent(int tupleSystemIndex, Component component)
		{
			int tupleIndex = m_ComponentLists[tupleSystemIndex].GetIndex(component);
			if (tupleIndex == -1)
				return;

			RemoveSwapBackTupleIndex(tupleIndex);
		}

		private void RemoveSwapBackTupleIndex(int tupleIndex)
		{
			if (m_ChangeEvent != null)
				m_ChangeEvent.OnRemoveSwapBack (tupleIndex);

			for (int i = 0; i != m_ComponentLists.Length; i++)
				m_ComponentLists[i].RemoveAtSwapBackComponent (tupleIndex);

			for (int i = 0; i != m_ComponentDataIndices.Length; i++)
				m_ComponentDataIndices[i].RemoveAtSwapBack (tupleIndex);

			if (m_Transforms.IsCreated)
				m_Transforms.RemoveAtSwapBack(tupleIndex);

			var entity = m_TupleToEntityIndex[tupleIndex];
			m_EntityToTupleIndex.Remove (entity.index);
			m_TupleToEntityIndex.RemoveAtSwapBack (tupleIndex);

			if (tupleIndex != m_TupleToEntityIndex.Length)
			{
				var lastEntity = m_TupleToEntityIndex[tupleIndex];
				m_EntityToTupleIndex.Remove(lastEntity.index);
				m_EntityToTupleIndex.TryAdd (lastEntity.index, tupleIndex);
			}
		}

		public void AddTupleIfSupported(GameObject go, Entity lightGameObject)
		{
			if (!IsTupleSupported (go, lightGameObject))
				return;

			// Component injections
			for (int i = 0; i != m_ComponentTypes.Length; i++)
			{
				var component = go.GetComponent (m_ComponentTypes[i]);
				m_ComponentLists[i].AddComponent (component);
			}

			// IComponentData injections
			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{		
				int componentIndex = m_EntityManager.GetComponentIndex(lightGameObject, m_ComponentDataTypes[i]);
				Assert.AreNotEqual (-1, componentIndex);

				m_ComponentDataIndices[i].Add(componentIndex);
			}

			// Transform component injections
			if (m_Transforms.IsCreated)
				m_Transforms.Add(go.transform);

			// Tuple / Entity mapping
			int tupleIndex = m_TupleToEntityIndex.Length;
			m_EntityToTupleIndex.TryAdd (lightGameObject.index, tupleIndex);
			m_TupleToEntityIndex.Add (lightGameObject);

			if (m_ChangeEvent != null)
				m_ChangeEvent.OnAddElements (1);
		}

		public void AddTuplesEntityIDPartial(NativeArray<Entity> entityIndices)
		{
			int baseIndex = m_TupleToEntityIndex.Length;
			for (int i = 0;i<entityIndices.Length;i++)
			{
				m_TupleToEntityIndex.Add (entityIndices[i]);
				m_EntityToTupleIndex.TryAdd (entityIndices[i].index, baseIndex + i);
			}

			if (m_ChangeEvent != null)
				m_ChangeEvent.OnAddElements (entityIndices.Length);
		}

		public void AddTuplesComponentDataPartial(int componentTypeIndex, NativeSlice<int> componentIndices)
		{
			int tupleIndex = System.Array.IndexOf (m_ComponentDataTypes, componentTypeIndex);
			if (tupleIndex == -1)
				return;

			var tuplesIndices = m_ComponentDataIndices[tupleIndex];

			int count = componentIndices.Length;
			tuplesIndices.ResizeUninitialized (tuplesIndices.Length + count);
			var indices = new NativeSlice<int> (tuplesIndices, tuplesIndices.Length - count);
			indices.CopyFrom (componentIndices);
		}
		#endif // ECS_ENTITY_CLASS

		public void Dispose()
		{

			#if ECS_ENTITY_CLASS
			for (int i = 0; i != m_ComponentDataIndexSegments.Length; i++)
				m_ComponentDataIndexSegments[i].Dispose();
			#else
			for (int i = 0; i != m_ComponentDataIndices.Length; i++)
				m_ComponentDataIndices[i].Dispose();
			#endif

			//@TODO: Shouldn't dispose check this itself???
			if (m_Transforms.IsCreated)
				m_Transforms.Dispose ();

			m_EntityToTupleIndex.Dispose();
			m_TupleToEntityIndex.Dispose();
		}

		public Type[] Types
		{
			get
			{
				var types = new List<Type> ();
				if (m_Transforms.IsCreated)
					types.Add (typeof(TransformAccessArray));
				foreach(var typeIndex in m_ComponentDataTypes)
					#if ECS_ENTITY_CLASS
					types.Add(m_EntityManager.GetTypeFromIndex (typeIndex));
					#else
					types.Add(EntityManager.GetTypeFromIndex (typeIndex));
					#endif
				types.AddRange (m_ComponentTypes);

				return types.ToArray ();
			}
		}

		public void AddChangeEventListener (IEntityGroupChange evt)
		{
			m_ChangeEvent = evt;
		}
		#if ECS_ENTITY_CLASS
		List<EntityClass> m_MatchingClasses = new List<EntityClass>();
		internal void AddClassIfMatching(EntityClass entityClass)
		{
			if (m_ComponentDataTypes.Length > entityClass.componentTypes.Length)
				return;
			if (m_Transforms.IsCreated && !entityClass.hasTransform)
				return;
			int matches = 0;
			for (int i = 0; i < m_ComponentDataTypes.Length; ++i)
			{
				for (int j = 0; j < entityClass.componentTypes.Length; ++j)
				{
					if (m_ComponentDataTypes[i] == entityClass.componentTypes[j])
						++matches;
				}
			}
			int targetMatches = m_ComponentDataTypes.Length;
			if (matches == targetMatches);
				m_MatchingClasses.Add(entityClass);
		}
		#endif
	}
}