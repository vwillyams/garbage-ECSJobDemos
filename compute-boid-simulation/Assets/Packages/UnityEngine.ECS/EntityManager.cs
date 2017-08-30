using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

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
		List<Type> 										  m_ComponentTypes = new List<Type>();
		List<IComponentDataManager> 				      m_ComponentManagers = new List<IComponentDataManager>();
		List<List<EntityGroup.RegisteredTuple> > 		  m_EntityGroupsForComponent = new List<List<EntityGroup.RegisteredTuple> >();

		NativeMultiHashMap<int, LightWeightComponentInfo> m_EntityToComponent;

    	int 											  m_InstanceIDAllocator = -1;
    	int 										      m_DebugManagerID;

    	struct LightWeightComponentInfo
    	{
    		public int  componentTypeIndex;
    		// Index of the component in the LightWeightComponentManager
    		public int 	index;
    	}

    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

			m_EntityToComponent = new NativeMultiHashMap<int, LightWeightComponentInfo> (capacity, Allocator.Persistent);
    		m_DebugManagerID = 1;
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
    		m_EntityToComponent.Dispose ();
    	}

		internal Type GetTypeFromIndex(int index)
		{
			return m_ComponentTypes[index];
		}

    	internal int GetTypeIndex(Type type)
    	{
    		//@TODO: Initialize with all types on startup instead? why continously populate...
    		for (int i = 0; i < m_ComponentTypes.Count; i++)
    		{
    			if (m_ComponentTypes [i] == type)
    				return i;
    		}

    		if (!typeof(IComponentData).IsAssignableFrom (type))
				throw new ArgumentException (string.Format("{0} must be a IComponentData to be used when create a lightweight game object", type));
    		
    		m_ComponentTypes.Add (type);
			m_EntityGroupsForComponent.Add (new List<EntityGroup.RegisteredTuple>());

			var managerType = typeof(ComponentDataManager<>).MakeGenericType(new Type[] { type });
			var manager = DependencyManager.GetBehaviourManager (managerType) as IComponentDataManager;
			m_ComponentManagers.Add (manager);

    		return m_ComponentTypes.Count - 1;
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
    		return GetComponentIndex (entity, GetTypeIndex(typeof(T)));
    	}

    	public int GetComponentIndex(Entity entity, Type type)
    	{
    		return GetComponentIndex (entity, GetTypeIndex(type));
    	}

		public bool HasComponent<T>(Entity entity) where T : IComponentData
		{
			return GetComponentIndex (entity, GetTypeIndex(typeof(T))) != -1;
		}

		public bool HasComponent(Entity entity, Type type)
		{
			return GetComponentIndex (entity, GetTypeIndex(type)) != -1;
		}

		public Entity AllocateEntity()
		{
			var go = new Entity(m_DebugManagerID, m_InstanceIDAllocator);
			m_InstanceIDAllocator -= 2;
			return go;
		}

		public void AddComponent<T>(Entity entity, T componentData) where T : struct, IComponentData
		{
			Assert.IsFalse (HasComponent<T>(entity));

			// Add to manager
			var manager = GetComponentManager<T> ();
			int index = manager.AddElement (componentData);

			// game object lookup table
			LightWeightComponentInfo info;
			info.componentTypeIndex = GetTypeIndex (typeof(T));
			info.index = index;
			m_EntityToComponent.Add(entity.index, info);

			var fullGameObject = UnityEditor.EditorUtility.InstanceIDToObject (entity.index) as GameObject;

			// tuple management
			foreach (var tuple in m_EntityGroupsForComponent[info.componentTypeIndex])
				tuple.tupleSystem.AddTupleIfSupported(fullGameObject, entity);
		}
			
    	public T GetComponent<T>(Entity entity) where T : struct, IComponentData
    	{
    		int index = GetComponentIndex<T> (entity);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> ();
			manager.CompleteForReading ();
			return manager.m_Data[index];
    	}

    	public void SetComponent<T>(Entity entity, T componentData) where T: struct, IComponentData
    	{
    		int index = GetComponentIndex<T> (entity);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> ();
			manager.CompleteForWriting ();
    		manager.m_Data[index] = componentData;
    	}

    	ComponentDataManager<T> GetComponentManager<T>() where T: struct, IComponentData
    	{
    		return DependencyManager.GetBehaviourManager (typeof(ComponentDataManager<T>)) as ComponentDataManager<T>;
    	}

    	//@TODO: Need overload with the specific components to clone somehow???
    	public NativeArray<Entity> Instantiate (GameObject gameObject, int numberOfInstances)
    	{
    		if (numberOfInstances < 1)
    			throw new System.ArgumentException ("Number of instances must be greater than 1");

    		var components = gameObject.GetComponents<ComponentDataWrapperBase> ();
			//@TODO: Temp alloc
			var componentDataTypes = new NativeArray<int> (components.Length, Allocator.Persistent);
    		for (int t = 0;t != components.Length;t++)
				componentDataTypes[t] = GetTypeIndex(components[t].GetIComponentDataType());

    		//@TODO: Temp alloc
			var entities = new NativeArray<Entity> (numberOfInstances, Allocator.Persistent);
			var allComponentIndices = new NativeArray<int> (numberOfInstances * components.Length, Allocator.Persistent);

    		for (int t = 0;t != components.Length;t++)
    		{
				var manager = m_ComponentManagers[componentDataTypes[t]];
				manager.AddElements (gameObject, new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
    		}

    		for (int t = 0; t != components.Length; t++)
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

			for (int i = 0; i < entities.Length; i++)
				entities[i] = new Entity (m_DebugManagerID, m_InstanceIDAllocator - i * 2);

			m_InstanceIDAllocator -= numberOfInstances * 2;


    		// Collect all tuples that support the created game object schema
    		var tuples = new HashSet<EntityGroup> ();
    		for (int t = 0;t != components.Length;t++)
				CollectComponentDataTupleSet (componentDataTypes[t], componentDataTypes, tuples);

    		foreach (var tuple in tuples)
    		{
    			for (int t = 0; t != components.Length; t++)
					tuple.AddTuplesComponentDataPartial(componentDataTypes[t], new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));

				tuple.AddTuplesEntityIDPartial (entities);
    		}
							
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

		//@TODO: Add HashMap functionality to remove a single component
		int RemoveComponentFromEntityTable(Entity entity, int typeIndex)
		{
			var components = new NativeList<LightWeightComponentInfo> (16, Allocator.Temp);
			int removedComponentIndex = -1;

			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;

			if (!m_EntityToComponent.TryGetFirstValue (entity.index, out component, out iterator))
			{
				components.Dispose ();
				throw new ArgumentException ("RemoveComponent may not be invoked on a game object that does not exist");
			}

			if (component.componentTypeIndex != typeIndex)
				components.Add (component);
			else
				removedComponentIndex = component.index;
			

			while (m_EntityToComponent.TryGetNextValue(out component, ref iterator))
			{
				if (component.componentTypeIndex != typeIndex)
					components.Add (component);
				else
					removedComponentIndex = component.index;
			}

			m_EntityToComponent.Remove (entity.index);
			for (int i = 0; i != components.Length;i++)
				m_EntityToComponent.Add (entity.index, components[i]);

			components.Dispose ();

			return removedComponentIndex;
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

			int componentTypeIndex = GetTypeIndex (typeof(T));
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