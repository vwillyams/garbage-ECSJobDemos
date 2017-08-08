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
		List<List<TupleSystem.RegisteredTuple> > 		  m_TuplesForComponent = new List<List<TupleSystem.RegisteredTuple> >();

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
			m_TuplesForComponent.Add (new List<TupleSystem.RegisteredTuple>());

			var managerType = typeof(ComponentDataManager<>).MakeGenericType(new Type[] { type });
			var manager = DependencyManager.GetBehaviourManager (managerType) as IComponentDataManager;
			m_ComponentManagers.Add (manager);

    		return m_ComponentTypes.Count - 1;
    	}

		internal int GetComponentIndex(Entity gameObject, int typeIndex)
		{
			//@TODO: debugManagerIndex validation

			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;
			if (!m_EntityToComponent.TryGetFirstValue (gameObject.index, out component, out iterator))
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

    	public int GetComponentIndex<T>(Entity gameObject) where T : IComponentData
    	{
    		return GetComponentIndex (gameObject, GetTypeIndex(typeof(T)));
    	}

    	public int GetComponentIndex(Entity gameObject, Type type)
    	{
    		return GetComponentIndex (gameObject, GetTypeIndex(type));
    	}

		public bool HasComponent<T>(Entity gameObject) where T : IComponentData
		{
			return GetComponentIndex (gameObject, GetTypeIndex(typeof(T))) != -1;
		}

		public bool HasComponent(Entity gameObject, Type type)
		{
			return GetComponentIndex (gameObject, GetTypeIndex(type)) != -1;
		}

		public Entity AllocateEntity()
		{
			var go = new Entity(m_DebugManagerID, m_InstanceIDAllocator);
			m_InstanceIDAllocator -= 2;
			return go;
		}

		public void AddComponent<T>(Entity gameObject, T componentData) where T : struct, IComponentData
		{
			Assert.IsFalse (HasComponent<T>(gameObject));

			// Add to manager
			var manager = GetComponentManager<T> ();
			int index = manager.AddElement (componentData);

			// game object lookup table
			LightWeightComponentInfo info;
			info.componentTypeIndex = GetTypeIndex (typeof(T));
			info.index = index;
			m_EntityToComponent.Add(gameObject.index, info);

			var fullGameObject = UnityEditor.EditorUtility.InstanceIDToObject (gameObject.index) as GameObject;

			// tuple management
			foreach (var tuple in m_TuplesForComponent[info.componentTypeIndex])
				tuple.tupleSystem.AddTupleIfSupported(fullGameObject, gameObject);
		}
			
    	public T GetComponent<T>(Entity gameObject) where T : struct, IComponentData
    	{
    		int index = GetComponentIndex<T> (gameObject);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

			var manager = GetComponentManager<T> ();
			manager.CompleteForReading ();
			return manager.m_Data[index];
    	}

    	public void SetComponent<T>(Entity gameObject, T componentData) where T: struct, IComponentData
    	{
    		int index = GetComponentIndex<T> (gameObject);
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
			var gameObjects = new NativeArray<Entity> (numberOfInstances, Allocator.Persistent);
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

			for (int i = 0; i < gameObjects.Length; i++)
				gameObjects[i] = new Entity (m_DebugManagerID, m_InstanceIDAllocator - i * 2);

			m_InstanceIDAllocator -= numberOfInstances * 2;


    		// Collect all tuples that support the created game object schema
    		var tuples = new HashSet<TupleSystem> ();
    		for (int t = 0;t != components.Length;t++)
				CollectComponentDataTupleSet (componentDataTypes[t], componentDataTypes, tuples);

    		foreach (var tuple in tuples)
    		{
    			for (int t = 0; t != components.Length; t++)
					tuple.AddTuplesUnchecked(componentDataTypes[t], new NativeSlice<int>(allComponentIndices, t * numberOfInstances, numberOfInstances));
    		}

			allComponentIndices.Dispose();
			componentDataTypes.Dispose ();

    		return gameObjects;
    	}

		void CollectComponentDataTupleSet(int typeIndex, NativeArray<int> requiredComponentTypes, HashSet<TupleSystem> tuples)
    	{
			foreach (var tuple in m_TuplesForComponent[typeIndex])
    		{
				if (tuple.tupleSystem.IsComponentDataTypesSupported(requiredComponentTypes))
    				tuples.Add (tuple.tupleSystem);
    		}
    	}

    	public void Destroy (Entity gameObject)
    	{
    		var temp = new NativeArray<Entity> (1, Allocator.Persistent);
    		temp [0] = gameObject;
    		Destroy(temp);
    		temp.Dispose ();
    	}

    	public Entity GameObjectToEntity(GameObject go)
    	{
    		Entity light;
    		light.debugManagerIndex = m_DebugManagerID;
			light.index = go.GetInstanceID();

    		return light;
    	}

		//@TODO: Add HashMap functionality to remove a single component
		int RemoveComponentFromGameObjectTable(Entity gameObject, int typeIndex)
		{
			var components = new NativeList<LightWeightComponentInfo> (16, Allocator.Temp);
			int removedComponentIndex = -1;

			LightWeightComponentInfo component;
			NativeMultiHashMapIterator<int> iterator;

			if (!m_EntityToComponent.TryGetFirstValue (gameObject.index, out component, out iterator))
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

			m_EntityToComponent.Remove (gameObject.index);
			for (int i = 0; i != components.Length;i++)
				m_EntityToComponent.Add (gameObject.index, components[i]);

			components.Dispose ();

			return removedComponentIndex;
		}

		// * NOTE: Does not modify m_GameObjectToComponent
		void RemoveComponentFromManagerAndTuples (NativeArray<int> components, int componentTypeIndex)
		{
			var manager = m_ComponentManagers[componentTypeIndex];
			manager.RemoveElements (components);

			foreach (var tuple in m_TuplesForComponent[componentTypeIndex])
			{
				for (int i = 0; i != components.Length;i++)
					tuple.tupleSystem.RemoveSwapBackLightWeightComponent (tuple.tupleSystemIndex, components [i]);
			}
		}

		public void RemoveComponent<T>(Entity gameObject) where T : struct, IComponentData
		{
			Assert.IsTrue (HasComponent<T>(gameObject));

			int componentTypeIndex = GetTypeIndex (typeof(T));
			int componentIndex = RemoveComponentFromGameObjectTable (gameObject, componentTypeIndex);

			var components = new NativeArray<int> (1, Allocator.Persistent);
			components[0] = componentIndex;

			RemoveComponentFromManagerAndTuples (components, componentTypeIndex);

			components.Dispose ();
		}

    	public void Destroy (NativeArray<Entity> gameObjects)
    	{
			var array = new NativeArray<int> (1, Allocator.Persistent);

    		for (int i = 0; i < gameObjects.Length; i++)
    		{
    			var gameObject = gameObjects[i];

    			//@TODO: Validate manager index...

    			LightWeightComponentInfo component;
    			NativeMultiHashMapIterator<int> iterator;
    			if (!m_EntityToComponent.TryGetFirstValue (gameObject.index, out component, out iterator))
    				throw new System.InvalidOperationException ("GameObject does not exist");

    			array[0] = component.index;
				RemoveComponentFromManagerAndTuples (array, component.componentTypeIndex);

    			while (m_EntityToComponent.TryGetNextValue(out component, ref iterator))
    			{
					array[0] = component.index;
					RemoveComponentFromManagerAndTuples (array, component.componentTypeIndex);
    			}

				m_EntityToComponent.Remove(gameObject.index);
    		}
    		
    		array.Dispose ();
    	}

		internal void RegisterTuple(int componentTypeIndex, TupleSystem tuple, int tupleSystemIndex)
		{
			m_TuplesForComponent [componentTypeIndex].Add (new TupleSystem.RegisteredTuple (tuple, tupleSystemIndex));
		}
    }
}