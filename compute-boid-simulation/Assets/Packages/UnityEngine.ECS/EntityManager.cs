using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;
using UnityEngine.Profiling;

namespace UnityEngine.ECS
{
    public struct Entity
    {
    	internal int debugManagerIndex;
    	internal int index;

    	internal Entity(int debugManagerIndex, int index)
    	{
    		this.debugManagerIndex = debugManagerIndex;
    		this.index = index;
    	}
    }

    public class EntityManager : ScriptBehaviourManager
    {
		struct ComponentTypeCache<T>
		{
			public static int index;
		}

		struct LightWeightComponentInfo
		{
			public int  componentTypeIndex;
			// Index of the component in the LightWeightComponentManager
			public int 	index;
		}

		static List<Type> 								  ms_ComponentTypes;
		static List<EntityManager> 						  ms_AllEntityManagers;

		List<IComponentDataManager> 					  m_ComponentManagers;
		List<List<EntityGroup.RegisteredTuple> > m_EntityGroupsForComponent;

		NativeMultiHashMap<int, LightWeightComponentInfo> m_EntityToComponent;

    	int 											  m_InstanceIDAllocator = -1;
    	int 										      m_DebugManagerID;

		CustomSampler m_AddToEntityComponentTable;
		CustomSampler m_AddToEntityGroup;
		CustomSampler m_AddComponentManagerElements;
		CustomSampler m_AddToEntityGroup1;
		CustomSampler m_AddToEntityGroup2;


    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

			m_EntityToComponent = new NativeMultiHashMap<int, LightWeightComponentInfo> (capacity, Allocator.Persistent);
    		m_DebugManagerID = 1;

			if (ms_ComponentTypes == null)
			{
				ms_ComponentTypes = new List<Type> ();
				ms_ComponentTypes.Add (null);

				ms_AllEntityManagers = new List<EntityManager> ();
			}

			ms_AllEntityManagers.Add (this);

			m_ComponentManagers = new List<IComponentDataManager>(ms_ComponentTypes.Count);
			m_EntityGroupsForComponent = new List<List<EntityGroup.RegisteredTuple> >(ms_ComponentTypes.Count);

			foreach (var type in ms_ComponentTypes)
			{
				m_ComponentManagers.Add (null);
				m_EntityGroupsForComponent.Add (new List<EntityGroup.RegisteredTuple>());
			}

			m_AddToEntityComponentTable = CustomSampler.Create ("AddToEntityComponentTable"); ;
			m_AddToEntityGroup = CustomSampler.Create ("AddToEntityGroup"); 
			m_AddComponentManagerElements = CustomSampler.Create ("AddComponentManagerElements"); 
			m_AddToEntityGroup1 = CustomSampler.Create ("1"); 
			m_AddToEntityGroup2 = CustomSampler.Create ("2"); 
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
    		m_EntityToComponent.Dispose ();
			m_ComponentManagers = null;
			m_EntityGroupsForComponent = null;

			ms_AllEntityManagers.Remove (this);
    	}

		internal static Type GetTypeFromIndex(int index)
		{
			return ms_ComponentTypes[index];
		}

		internal int GetTypeIndex<T>()
		{
			int typeIndex = ComponentTypeCache<T>.index;
			if (typeIndex != 0)
				return typeIndex;

			typeIndex = GetTypeIndex (typeof(T));
			ComponentTypeCache<T>.index = typeIndex;
			return typeIndex;
		}

    	internal int GetTypeIndex(Type type)
    	{
    		//@TODO: Initialize with all types on startup instead? why continously populate...
    		for (int i = 0; i < ms_ComponentTypes.Count; i++)
    		{
    			if (ms_ComponentTypes[i] == type)
    				return i;
    		}

    		if (!typeof(IComponentData).IsAssignableFrom (type))
				throw new ArgumentException (string.Format("{0} must be a IComponentData to be used when create a lightweight game object", type));

			ms_ComponentTypes.Add (type);

			foreach (var manager in ms_AllEntityManagers)
			{
				manager.m_EntityGroupsForComponent.Add (new List<EntityGroup.RegisteredTuple>());
				manager.m_ComponentManagers.Add (null);
			}

    		return ms_ComponentTypes.Count - 1;
    	}

		internal int GetComponentIndex(Entity entity, int typeIndex)
		{
			//@TODO: debugManagerIndex validation

			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;
			if (!m_EntityToComponent.TryGetFirstValue (entity.index, out component, out iterator))
				return -1;

			if (component.componentTypeIndex == typeIndex)
				return component.index;

			//@TODO: Why do i need if + while... very inconvenient...
			while (m_EntityToComponent.TryGetNextValue(out component, ref iterator))
			{
				if (component.componentTypeIndex == typeIndex)
					return component.index;
			}

			return -1;
		}

    	public int GetComponentIndex<T>(Entity entity) where T : IComponentData
    	{
			return GetComponentIndex (entity, GetTypeIndex<T>());
    	}

    	public int GetComponentIndex(Entity entity, Type type)
    	{
    		return GetComponentIndex (entity, GetTypeIndex(type));
    	}

		public bool HasComponent<T>(Entity entity) where T : IComponentData
		{
			return GetComponentIndex (entity, GetTypeIndex<T>()) != -1;
		}

		public bool HasComponent(Entity entity, Type type)
		{
			return GetComponentIndex (entity, GetTypeIndex(type)) != -1;
		}

		bool HasComponent(Entity entity, int typeIndex)
		{
			return GetComponentIndex (entity, typeIndex) != -1;
		}

		void GetComponentTypes(Entity entity, NativeList<int> componentTypes)
		{
			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;

			if (m_EntityToComponent.TryGetFirstValue (entity.index, out component, out iterator))
			{
				componentTypes.Add(component.componentTypeIndex);

				while (m_EntityToComponent.TryGetNextValue(out component, ref iterator))
				{
					componentTypes.Add(component.componentTypeIndex);
				}
			}
		}

		public Entity AllocateEntity()
		{
			var go = new Entity(m_DebugManagerID, m_InstanceIDAllocator);
			m_InstanceIDAllocator -= 2;
			return go;
		}

		public void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
		{
			int typeIndex = GetTypeIndex<T>();
			Assert.IsFalse (HasComponent(entity, typeIndex));

			// Add to manager
			var manager = GetComponentManager<T> (typeIndex);
			int index = manager.AddElement (componentData);

			// game object lookup table
			LightWeightComponentInfo info;
			info.componentTypeIndex = typeIndex;
			info.index = index;
			m_EntityToComponent.Add(entity.index, info);

			var fullGameObject = UnityEditor.EditorUtility.InstanceIDToObject (entity.index) as GameObject;

			// tuple management
			foreach (var tuple in m_EntityGroupsForComponent[typeIndex])
				tuple.tupleSystem.AddTupleIfSupported(fullGameObject, entity);
		}
			
    	public T GetComponent<T>(Entity entity) where T : struct, IComponentData
    	{
			int typeIndex = GetTypeIndex<T> ();

			int index = GetComponentIndex (entity, typeIndex);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> (typeIndex);
			manager.CompleteForReading ();
			return manager.m_Data[index];
    	}

    	public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
    	{
			int typeIndex = GetTypeIndex<T> ();

			int index = GetComponentIndex (entity, typeIndex);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> (typeIndex);
			manager.CompleteForWriting ();
    		manager.m_Data[index] = componentData;
    	}

		ComponentDataManager<T> GetComponentManager<T>(int typeIndex) where T: struct, IComponentData
    	{
			if (m_ComponentManagers [typeIndex] == null)
				GetComponentManager(typeIndex);

			return (ComponentDataManager<T>)m_ComponentManagers[typeIndex];
    	}

		IComponentDataManager GetComponentManager(int typeIndex)
		{
			if (m_ComponentManagers[typeIndex] == null)
			{
				var managerType = typeof(ComponentDataManager<>).MakeGenericType(new Type[] { ms_ComponentTypes[typeIndex] });
				m_ComponentManagers[typeIndex] = DependencyManager.GetBehaviourManager (managerType) as IComponentDataManager;
			}

			return m_ComponentManagers[typeIndex];
		}


		NativeArray<Entity> InstantiateCompleteCreation (NativeArray<int> componentDataTypes, NativeArray<int> allComponentIndices, int numberOfInstances)
		{
			m_AddToEntityComponentTable.Begin ();

			for (int t = 0; t != componentDataTypes.Length; t++)
			{
				//@TOOD: Batchable
				LightWeightComponentInfo componentInfo;
				componentInfo.componentTypeIndex = componentDataTypes[t];

				for (int g = 0; g != numberOfInstances; g++)
				{
					componentInfo.index = allComponentIndices[g + t * numberOfInstances];
					m_EntityToComponent.Add (m_InstanceIDAllocator - g * 2, componentInfo);
				}
			}
			m_AddToEntityComponentTable.End ();

			var entities = new NativeArray<Entity> (numberOfInstances, Allocator.Temp);

			for (int i = 0; i < entities.Length; i++)
				entities[i] = new Entity (m_DebugManagerID, m_InstanceIDAllocator - i * 2);

			m_InstanceIDAllocator -= numberOfInstances * 2;


			m_AddToEntityGroup.Begin ();

			// Collect all tuples that support the created game object schema
			var tuples = new HashSet<EntityGroup> ();
			for (int t = 0;t != componentDataTypes.Length;t++)
				CollectComponentDataTupleSet (componentDataTypes[t], componentDataTypes, tuples);

			foreach (var tuple in tuples)
			{
				m_AddToEntityGroup1.Begin ();
				for (int t = 0; t != componentDataTypes.Length; t++)
					tuple.AddTuplesComponentDataPartial(componentDataTypes[t], new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
				m_AddToEntityGroup1.End ();

				m_AddToEntityGroup2.Begin ();
				tuple.AddTuplesEntityIDPartial (entities);
				m_AddToEntityGroup2.End ();
			}

			m_AddToEntityGroup.End ();
				
			return entities;
		}


		public NativeArray<Entity> Instantiate (Entity entity, int numberOfInstances)
		{
			if (numberOfInstances < 1)
				throw new System.ArgumentException ("Number of instances must be greater or equal to 1");

			var componentDataTypesList = new NativeList<int> (10, Allocator.Temp);
			GetComponentTypes (entity, componentDataTypesList);

			//@TODO: derived NativeArray is not allowed to be deallocated...
			NativeArray<int> componentDataTypes = componentDataTypesList;

			//@TODO: Temp alloc
			var allComponentIndices = new NativeArray<int> (numberOfInstances * componentDataTypes.Length, Allocator.Temp);

			for (int t = 0;t != componentDataTypes.Length;t++)
			{
				var manager = GetComponentManager (componentDataTypes[t]);
				manager.AddElements (GetComponentIndex(entity, componentDataTypes[t]), new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
			}

			var entities = InstantiateCompleteCreation (componentDataTypes, allComponentIndices, numberOfInstances);

			allComponentIndices.Dispose();
			componentDataTypesList.Dispose ();

			return entities;
		}

    	//@TODO: Need overload with the specific components to clone somehow???
    	public NativeArray<Entity> Instantiate (GameObject gameObject, int numberOfInstances)
    	{
    		if (numberOfInstances < 1)
    			throw new System.ArgumentException ("Number of instances must be greater or equal to 1");

    		var components = gameObject.GetComponents<ComponentDataWrapperBase> ();
			//@TODO: Temp alloc
			var componentDataTypes = new NativeArray<int> (components.Length, Allocator.Temp);
    		for (int t = 0;t != components.Length;t++)
				componentDataTypes[t] = GetTypeIndex(components[t].GetIComponentDataType());

    		//@TODO: Temp alloc
			var allComponentIndices = new NativeArray<int> (numberOfInstances * components.Length, Allocator.Temp);

			m_AddComponentManagerElements.Begin ();
    		for (int t = 0;t != components.Length;t++)
    		{
				var manager = GetComponentManager (componentDataTypes [t]);
				manager.AddElements (gameObject, new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
    		}
			m_AddComponentManagerElements.End ();

			var entities = InstantiateCompleteCreation (componentDataTypes, allComponentIndices, numberOfInstances);

			allComponentIndices.Dispose();
			componentDataTypes.Dispose ();

			return entities;
    	}

		void CollectComponentDataTupleSet(int typeIndex, NativeArray<int> requiredComponentTypes, HashSet<EntityGroup> tuples)
    	{
			foreach (var tuple in m_EntityGroupsForComponent[typeIndex])
    		{
				if (tuple.tupleSystem.IsComponentDataTypesSupported(requiredComponentTypes))
    				tuples.Add (tuple.tupleSystem);
    		}
    	}

		public void Destroy (NativeArray<Entity> entities)
    	{
			for (var i = 0;i != entities.Length;i++)
	    		Destroy(entities[i]);
    	}

    	public Entity GameObjectToEntity(GameObject go)
    	{
    		Entity light;
    		light.debugManagerIndex = m_DebugManagerID;
			light.index = go.GetInstanceID();

    		return light;
    	}

		int RemoveComponentFromEntityTable(Entity entity, int typeIndex)
		{
			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;
			if (!m_EntityToComponent.TryGetFirstValue (entity.index, out component, out iterator))
			{
				throw new ArgumentException ("RemoveComponent may not be invoked on a game object that does not exist");
			}
			do
			{
				if (component.componentTypeIndex == typeIndex)
				{
					m_EntityToComponent.Remove(iterator);
					return component.index;
				}
			} while (m_EntityToComponent.TryGetNextValue(out component, ref iterator));
			return -1;
		}

		// * NOTE: Does not modify m_EntityToComponent
		void RemoveEntityFromTuples (Entity entity, int componentTypeIndex)
		{
			foreach (var tuple in m_EntityGroupsForComponent[componentTypeIndex])
				tuple.tupleSystem.RemoveSwapBackComponentData(entity);
		}

		public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
		{
			Assert.IsTrue (HasComponent<T>(entity));

			int componentTypeIndex = GetTypeIndex<T> ();
			int componentIndex = RemoveComponentFromEntityTable (entity, componentTypeIndex);

			m_ComponentManagers[componentTypeIndex].RemoveElement (componentIndex);
			RemoveEntityFromTuples (entity, componentTypeIndex);
		}

    	public void Destroy (Entity entity)
    	{
			//@TODO: Validate manager index...

			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;
			if (!m_EntityToComponent.TryGetFirstValue (entity.index, out component, out iterator))
				throw new System.InvalidOperationException ("Entity does not exist");

			// Remove Component Data
			m_ComponentManagers[component.componentTypeIndex].RemoveElement(component.index);

			foreach (var tuple in m_EntityGroupsForComponent[component.componentTypeIndex])
				tuple.tupleSystem.RemoveSwapBackComponentData(entity);

			while (m_EntityToComponent.TryGetNextValue(out component, ref iterator))
			{
				m_ComponentManagers[component.componentTypeIndex].RemoveElement(component.index);

				foreach (var tuple in m_EntityGroupsForComponent[component.componentTypeIndex])
					tuple.tupleSystem.RemoveSwapBackComponentData(entity);
			}

			m_EntityToComponent.Remove(entity.index);
    	}

		internal void RegisterTuple(int componentTypeIndex, EntityGroup tuple, int tupleSystemIndex)
		{
			m_EntityGroupsForComponent [componentTypeIndex].Add (new EntityGroup.RegisteredTuple (tuple, tupleSystemIndex));
		}
    }
}