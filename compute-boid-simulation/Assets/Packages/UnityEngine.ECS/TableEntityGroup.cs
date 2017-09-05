using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Assertions;

namespace UnityEngine.ECS
{
    #if ECS_ENTITY_TABLE

	public interface IEntityGroupChange
	{
		void OnAddElements (int numberOfEntitiesAddedToGroup);
		void OnRemoveSwapBack (int indexOfEntityToBeRemoved);
	}

	public class EntityGroup
	{
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

        int                             m_GroupIndex;

		IEntityGroupChange 				m_ChangeEvent;

		// Transforms
		TransformAccessArray	 		m_Transforms;

		// ComponentData
		int[]                    		m_ComponentDataTypes;
		NativeList<int>[]       		m_ComponentDataIndices;
		ScriptBehaviourManager[] 		m_ComponentDataManagers;
		EntityManager 			 		m_EntityManager;

		// ComponentType
		Type[]                   		m_ComponentTypes;
		IGenericComponentList[] 		m_ComponentLists;

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
			m_ComponentDataIndices = new NativeList<int>[componentDataTypes.Length];
			m_ComponentDataManagers = componentDataManagers;
			m_ComponentDataTypes = new int[componentDataTypes.Length];
			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{
				m_ComponentDataIndices[i] = new NativeList<int>(0, Allocator.Persistent);
				m_ComponentDataTypes[i] = entityManager.GetTypeIndex(componentDataTypes[i]);
			}

			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{
				m_EntityManager.RegisterTuple (m_ComponentDataTypes[i], this, i);
			}

            m_GroupIndex = m_EntityManager.AddEntityGroup (this);
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

		internal ComponentDataArray<T> GetComponentDataArray<T>(int index, bool readOnly) where T : struct, IComponentData
		{
			var manager = m_ComponentDataManagers[index] as ComponentDataManager<T>;


			var container = new ComponentDataArray<T> (manager.m_Data, m_ComponentDataIndices[index], readOnly);
			return container;
		}

		internal ComponentArray<T> GetComponentArray<T>(int index) where T : Component
		{
			ComponentArray<T> array;
			array.m_List = (List<T>)m_ComponentLists[index];
			return array;
		}

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
            int tupleIndex = m_EntityManager.GetIndexInGroup (entity, m_GroupIndex);

			if (tupleIndex == -1)
				return;

			RemoveSwapBackTupleIndex(tupleIndex, true);
		}

		public void RemoveSwapBackComponent(int tupleSystemIndex, Component component)
		{
			int tupleIndex = m_ComponentLists[tupleSystemIndex].GetIndex(component);
			if (tupleIndex == -1)
				return;

			RemoveSwapBackTupleIndex(tupleIndex, true);
		}

        internal void RemoveSwapBackTupleIndex(int tupleIndex, bool removeFromGroup)
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
			m_TupleToEntityIndex.RemoveAtSwapBack (tupleIndex);

            if (removeFromGroup)
                m_EntityManager.RemoveGroupFromEntity (entity, m_GroupIndex);

			if (tupleIndex != m_TupleToEntityIndex.Length)
			{
				var lastEntity = m_TupleToEntityIndex[tupleIndex];
                m_EntityManager.UpdateIndexInGroup (lastEntity, m_GroupIndex, tupleIndex);
			}
		}

		public unsafe void AddTupleIfSupported(GameObject go, Entity entity)
		{
			if (!IsTupleSupported (go, entity))
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
				int componentIndex = m_EntityManager.GetComponentIndex(entity, m_ComponentDataTypes[i]);
				Assert.AreNotEqual (-1, componentIndex);

				m_ComponentDataIndices[i].Add(componentIndex);
			}

			// Transform component injections
			if (m_Transforms.IsCreated)
				m_Transforms.Add(go.transform);

			// Tuple / Entity mapping
			int tupleIndex = m_TupleToEntityIndex.Length;
			m_TupleToEntityIndex.Add (entity);

            m_EntityManager.AddEntityToGroup (&entity, 1, m_GroupIndex, tupleIndex);

			if (m_ChangeEvent != null)
				m_ChangeEvent.OnAddElements (1);
		}

		public unsafe void AddTuplesEntityIDPartial(NativeArray<Entity> entityIndices)
		{
			int baseIndex = m_TupleToEntityIndex.Length;
            m_TupleToEntityIndex.AddRange (entityIndices);
            m_EntityManager.AddEntityToGroup ((Entity*)entityIndices.UnsafePtr, entityIndices.Length, m_GroupIndex, baseIndex);

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

		public void Dispose()
		{
			for (int i = 0; i != m_ComponentDataIndices.Length; i++)
				m_ComponentDataIndices[i].Dispose();

			//@TODO: Shouldn't dispose check this itself???
			if (m_Transforms.IsCreated)
				m_Transforms.Dispose ();

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
					types.Add(EntityManager.GetTypeFromIndex (typeIndex));
				types.AddRange (m_ComponentTypes);

				return types.ToArray ();
			}
		}

		public void AddChangeEventListener (IEntityGroupChange evt)
		{
			m_ChangeEvent = evt;
		}
	}
    #endif
}