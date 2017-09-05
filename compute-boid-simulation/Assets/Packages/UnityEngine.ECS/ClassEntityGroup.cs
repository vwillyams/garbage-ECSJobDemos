using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
	#if ECS_ENTITY_CLASS
	public interface IEntityGroupChange
	{
		void OnAddElements (int numberOfEntitiesAddedToGroup);
		void OnRemoveSwapBack (int indexOfEntityToBeRemoved);
	}

	public class EntityGroup
	{
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
		NativeList<ComponentDataIndexSegment>[]       		m_ComponentDataIndexSegments;
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
			m_ComponentDataIndexSegments = new NativeList<ComponentDataIndexSegment>[componentDataTypes.Length];
			m_ComponentDataManagers = componentDataManagers;
			m_ComponentDataTypes = new int[componentDataTypes.Length];
			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{
				m_ComponentDataIndexSegments[i] = new NativeList<ComponentDataIndexSegment>(0, Allocator.Persistent);
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
			m_TupleToEntityIndex.Clear();
			for (int i = 0; i < m_MatchingClasses.Count; ++i)
			{
				for (int j = 0; j < m_MatchingClasses[i].entities.Length; ++j)
					m_TupleToEntityIndex.Add(m_MatchingClasses[i].entities[j]);
			}
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

		internal unsafe ComponentDataArray<T> GetComponentDataArray<T>(int index, bool readOnly) where T : struct, IComponentData
		{
			var manager = m_ComponentDataManagers[index] as ComponentDataManager<T>;

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

			var container = new ComponentDataArray<T> (manager.m_Data, m_ComponentDataIndexSegments[index], readOnly);
			return container;
		}

		internal ComponentArray<T> GetComponentArray<T>(int index) where T : Component
		{
			ComponentArray<T> array;
			array.m_List = (List<T>)m_ComponentLists[index];
			return array;
		}

		public void Dispose()
		{
			for (int i = 0; i != m_ComponentDataIndexSegments.Length; i++)
				m_ComponentDataIndexSegments[i].Dispose();

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
	}
	#endif
}