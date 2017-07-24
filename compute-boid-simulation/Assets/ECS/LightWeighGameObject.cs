using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace ECS
{
    public struct LightweightGameObject
    {
    	internal int debugManagerIndex;
    	internal int index;

    	internal LightweightGameObject(int debugManagerIndex, int index)
    	{
    		this.debugManagerIndex = debugManagerIndex;
    		this.index = index;
    	}
    }

    public class LightweightGameObjectManager : ScriptBehaviourManager
    {
    	List<Type> m_ComponentTypes = new List<Type>();

    	NativeMultiHashMap<int, LightWeightComponentInfo> m_GameObjectToComponent;
    	int 											  m_InstanceIDAllocator = 1;
    	int 										      m_DebugManagerID;

    	NativeHashMap<int, int> 	  					  m_GameObjectInstanceIDToLightweightID;

    	struct LightWeightComponentInfo
    	{
    		public int  componentTypeIndex;
    		// Index of the component in the LightWeightComponentManager
    		public int 	index;
    	}


    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

			m_GameObjectToComponent = new NativeMultiHashMap<int, LightWeightComponentInfo> (capacity, Allocator.Persistent);
			m_GameObjectInstanceIDToLightweightID = new NativeHashMap<int, int> (capacity, Allocator.Persistent);
    		m_DebugManagerID = 1;
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
    		m_GameObjectToComponent.Dispose ();
    		m_GameObjectInstanceIDToLightweightID.Dispose ();
    	}


    	int GetTypeIndex(Type type)
    	{
    		//@TODO: Initialize with all types on startup instead? why continously populate...
    		for (int i = 0; i < m_ComponentTypes.Count; i++)
    		{
    			if (m_ComponentTypes [i] == type)
    				return i;
    		}

    		if (!typeof(IComponentData).IsAssignableFrom (type))
    			throw new ArgumentException (string.Format("{0} must be a ILightweightComponent to be used when create a lightweight game object", type));
    		
    		m_ComponentTypes.Add (type);
    		return m_ComponentTypes.Count - 1;
    	}

    	public int GetLightweightComponentIndex<T>(LightweightGameObject gameObject) where T : IComponentData
    	{
    		return GetComponentIndex (gameObject, GetTypeIndex(typeof(T)));
    	}

    	public int GetComponentIndex(LightweightGameObject gameObject, Type type)
    	{
    		return GetComponentIndex (gameObject, GetTypeIndex(type));
    	}

    	int GetComponentIndex(LightweightGameObject gameObject, int typeIndex)
    	{
    		//@TODO: debugManagerIndex validation

    		LightWeightComponentInfo component;
    		NativeMultiHashMapIterator<int> iterator;
    		if (!m_GameObjectToComponent.TryGetFirstValue (gameObject.index, out component, out iterator))
    			return -1;

    		if (component.componentTypeIndex == typeIndex)
    			return component.index;

    		//@TODO: Why do i need if + while... very inconvenient...
    		while (m_GameObjectToComponent.TryGetNextValue(out component, ref iterator))
    		{
    			if (component.componentTypeIndex == typeIndex)
    				return component.index;
    		}

    		return -1;
    	}


    	public T GetLightweightComponent<T>(LightweightGameObject gameObject) where T : struct, IComponentData
    	{
    		int index = GetLightweightComponentIndex<T> (gameObject);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

    		return GetComponentManager<T>().m_Data [index];
    	}

    	public void SetLightweightComponent<T>(LightweightGameObject gameObject, T componentData) where T: struct, IComponentData
    	{
    		int index = GetLightweightComponentIndex<T> (gameObject);
    		if (index == -1)
    			throw new InvalidOperationException (string.Format("{0} does not exist on the game object", typeof(T)));

    		GetComponentManager<T>().m_Data[index] = componentData;
    	}

    	LightweightComponentManager<T> GetComponentManager<T>() where T: struct, IComponentData
    	{
    		return DependencyManager.GetBehaviourManager (typeof(LightweightComponentManager<T>)) as LightweightComponentManager<T>;
    	}

    	//@TODO: Need overload with the specific components to clone somehow???
    	public NativeArray<LightweightGameObject> Instantiate (GameObject gameObject, int numberOfInstances)
    	{
    		if (numberOfInstances < 1)
    			throw new System.ArgumentException ("Number of instances must be greater than 1");

    		var components = gameObject.GetComponents<ComponentDataWrapperBase> ();
    		var lightweightComponentTypes = new Type[components.Length];
    		for (int t = 0;t != components.Length;t++)
    		{
    			if (components[t].m_Index == -1)
    				throw new InvalidOperationException (string.Format("{0} prefab component must be registered with a manager before instantiating", components.GetType()));

    			lightweightComponentTypes[t] = components [t].m_LightWeightType;
    		}

    		//@TODO: Temp alloc
    		var gameObjects = new NativeArray<LightweightGameObject> (numberOfInstances, Allocator.Persistent);

    		int baseID = m_InstanceIDAllocator;
    		m_InstanceIDAllocator += numberOfInstances;
    		for (int i = 0; i < gameObjects.Length; i++)
    			gameObjects[i] = new LightweightGameObject (m_DebugManagerID, baseID + i);

    		var firstAddedComponentIndices = new NativeArray<int> (components.Length, Allocator.Temp);
    		for (int t = 0;t != components.Length;t++)
    		{
    			var manager = GetLightweightComponentManager(GetTypeIndex(components[t].m_LightWeightType));
    			firstAddedComponentIndices[t] = manager.AddElements (manager, components[t].m_Index, gameObjects);
    		}

    		for (int t = 0; t != components.Length; t++)
    		{
    			int firstAddedComponentIndex = firstAddedComponentIndices [t];

    			//@TOOD: Batchable
    			LightWeightComponentInfo componentInfo;
    			componentInfo.componentTypeIndex = GetTypeIndex(components[t].m_LightWeightType);

    			for (int g = 0; g != numberOfInstances; g++)
    			{
    				componentInfo.index = firstAddedComponentIndex + g;
    				m_GameObjectToComponent.Add (baseID + g, componentInfo);
    			}
    		}
    			

    		// Collect all tuples that support the created game object schema
    		var tuples = new HashSet<TupleSystem> ();
    		for (int t = 0;t != components.Length;t++)
    		{
    			var manager = GetLightweightComponentManager(GetTypeIndex(components[t].m_LightWeightType));
    			manager.CollectSupportedTupleSets (lightweightComponentTypes, tuples);
    		}

    		foreach (var tuple in tuples)
    		{
    			for (int t = 0; t != components.Length; t++)
    				tuple.AddTuplesUnchecked(m_ComponentTypes[t], firstAddedComponentIndices[t], numberOfInstances);
    		}

    		firstAddedComponentIndices.Dispose();

    		return gameObjects;
    	}

    	ILightweightComponentManager GetLightweightComponentManager(int typeIndex)
    	{
    		var managerType = typeof(LightweightComponentManager<>).MakeGenericType(new Type[] { m_ComponentTypes[typeIndex] });
    		return DependencyManager.GetBehaviourManager (managerType)  as ILightweightComponentManager;
    	}

    	public LightweightGameObject Create (params Type[] types)
    	{
    		throw new System.NotImplementedException();
    	}

    	public void Destroy (LightweightGameObject gameObject)
    	{
    		var temp = new NativeArray<LightweightGameObject> (1, Allocator.Persistent);
    		temp [0] = gameObject;
    		Destroy(temp);
    		temp.Dispose ();
    	}

    	public LightweightGameObject GetLightweightGameObject(GameObject go)
    	{
    		LightweightGameObject light;
    		light.debugManagerIndex = m_DebugManagerID;
    		light.index = 0;
    		m_GameObjectInstanceIDToLightweightID.TryGetValue(go.GetInstanceID(), out light.index);

    		return light;
    	}

    	public int[] GetComponentTypeIndices(LightweightGameObject gameObject)
    	{
    		var types = new List<int>();
    		LightWeightComponentInfo component;
    		NativeMultiHashMapIterator<int> iterator;
    		if (!m_GameObjectToComponent.TryGetFirstValue (gameObject.index, out component, out iterator))
    			return types.ToArray();

    		types.Add(component.componentTypeIndex);
    		while (m_GameObjectToComponent.TryGetNextValue(out component, ref iterator))
    			types.Add(component.componentTypeIndex);

    		return types.ToArray();
    	}


    	public void MergeIntoFullGameObject(LightweightGameObject light, GameObject full)
    	{
    		//@TODO: Needs lots of work in defining behaviour of this... right now its just hack hack joy joy

    		foreach (var coms in full.GetComponents<ComponentDataWrapperBase>())
    			UnityEngine.Object.DestroyImmediate (coms);

    		m_GameObjectInstanceIDToLightweightID.TryAdd(full.GetInstanceID(), light.index);

    		var typeIndices = GetComponentTypeIndices (light);
    		var types = new Type[typeIndices.Length];
    		// Collect all tuples that support the created game object schema
    		var tuples = new HashSet<TupleSystem> ();
    		for (int t = 0;t != typeIndices.Length;t++)
    		{
    			types[t] = m_ComponentTypes[typeIndices[t]];
    			var manager = GetLightweightComponentManager(typeIndices[t]);
    			manager.CollectSupportedTupleSets (null, tuples);
    		}

    		foreach (var tuple in tuples)
    		{
    			// AddTupleIfSupported only if the light weight game object hasn't added into it already...
    			if (!tuple.IsLightWeightTupleSupported(types))
    				tuple.AddTupleIfSupported (full);
    		}
    	}

    	public void Destroy (NativeArray<LightweightGameObject> gameObjects)
    	{
    		var array = new NativeArray<int> (1, Allocator.Persistent);

    		for (int i = 0; i < gameObjects.Length; i++)
    		{
    			var gameObject = gameObjects[i];

    			//@TODO: Validate manager index...

    			LightWeightComponentInfo component;
    			NativeMultiHashMapIterator<int> iterator;
    			if (!m_GameObjectToComponent.TryGetFirstValue (gameObject.index, out component, out iterator))
    				throw new System.InvalidOperationException ("GameObject does not exist");

    			var manager = GetLightweightComponentManager(component.componentTypeIndex);
    			array[0] = component.index;
    			manager.RemoveElements (array);

    			while (m_GameObjectToComponent.TryGetNextValue(out component, ref iterator))
    			{
    				manager = GetLightweightComponentManager(component.componentTypeIndex);
    				array[0] = component.index;
    				manager.RemoveElements (array);
    			}

				m_GameObjectToComponent.Remove(gameObject.index);
    		}

    		array.Dispose ();
    	}
    }
}