using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;

namespace ECS
{
    public class TupleSystem
    {
    	internal class RegisteredTuple
    	{
    		public TupleSystem 	tupleSystem;
    		public int 			tupleSystemIndex;

    		public RegisteredTuple(TupleSystem tupleSystem, int tupleSystemIndex)
    		{
    			this.tupleSystemIndex = tupleSystemIndex;
    			this.tupleSystem = tupleSystem;
    		}
    	}

    	interface IGenericComponentListInjection
    	{
    		void AddComponent (Component com);
    		void RemoveAtSwapBackComponent (int index);
    		int GetIndex (Component com);
    	}

    	class GenericComponentListInjection<T> : List<T>, IGenericComponentListInjection where T : Component
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


        TransformAccessArray        		m_Transforms;
    	NativeList<int>[]           		m_TupleIndices;
    	IGenericComponentListInjection[]    m_TupleComponents;

    	Type[]                      		m_ComponentTypes;
    	ScriptBehaviourManager[]    		m_LightWeightManagers;
    	InjectTuples.TupleInjectionData[]   m_InjectionData;

    	internal TupleSystem(Type[] types, ScriptBehaviourManager[] lightweightManagers, InjectTuples.TupleInjectionData[] injectionData, TransformAccessArray transforms)
        {
            m_ComponentTypes = types;
    		this.m_LightWeightManagers = lightweightManagers;
    		this.m_Transforms = transforms;
    		this.m_InjectionData = injectionData;

    		m_TupleComponents = new IGenericComponentListInjection[types.Length];
            m_TupleIndices = new NativeList<int>[types.Length];

    		for (int i = 0; i != m_ComponentTypes.Length; i++)
    		{
    			var componentType = m_ComponentTypes[i];

    			if (componentType.IsSubclassOf(typeof(Component)))
    			{
    				var listType = typeof(GenericComponentListInjection<>).MakeGenericType(new Type[] { componentType });
    				m_TupleComponents[i] = (IGenericComponentListInjection)Activator.CreateInstance(listType);
    			}
    			else if (typeof(IComponentData).IsAssignableFrom(componentType))
    			{
                    m_TupleIndices[i] = new NativeList<int>(0, Allocator.Persistent);
    			}
    		}
        }

    	public void Dispose()
    	{
    		for (int i = 0; i != m_TupleIndices.Length; i++)
    		{
    			if (m_TupleIndices[i].IsCreated)
    				m_TupleIndices [i].Dispose();
    		}

    		//@TODO: Shouldn't dispose check this itself???
    		if (m_Transforms.IsCreated)
    			m_Transforms.Dispose ();
    	}

    	internal InjectTuples.TupleInjectionData[] InjectionData { get { return m_InjectionData; } }

    	public ComponentDataArray<T> GetLightWeightIndexedComponents<T>(int index, bool create) where T : struct, IComponentData
        {
    		var manager = m_LightWeightManagers[index] as LightweightComponentManager<T>;
    		var container = new ComponentDataArray<T> (manager.m_Data, m_TupleIndices[index]);

    		if (create)
    			manager.m_RegisteredTuples.Add(new RegisteredTuple(this, index));

            return container;
        }

        public ComponentArray<T> GetComponentContainer<T>(int index) where T : Component
    	{
			ComponentArray<T> array;
			array.m_List = (List<T>)m_TupleComponents[index];
			return array;
    	}

    	private void RemoveSwapBackTupleIndex(int tupleIndex)
        {
            for (int i = 0; i != m_TupleComponents.Length; i++)
            {
    			if (m_TupleComponents [i] != null)
    				m_TupleComponents [i].RemoveAtSwapBackComponent (tupleIndex);
    			if (m_TupleIndices[i].IsCreated)
    				m_TupleIndices[i].RemoveAtSwapBack (tupleIndex);
            }

    		if (m_Transforms.IsCreated)
    	        m_Transforms.RemoveAtSwapBack(tupleIndex);
    	}


    	public void RemoveSwapBackLightWeightComponent(int tupleSystemIndex, int componentIndex)
        {
			if (!m_TupleIndices[tupleSystemIndex].IsCreated)
				return;
            int tupleIndex = -1;
    		for (int i = 0; i != m_TupleIndices[tupleSystemIndex].Length; i++)
            {
    			if (m_TupleIndices[tupleSystemIndex][i] == componentIndex)
                    tupleIndex = i;
            }

    		if (tupleIndex == -1)
    			return;

    		var thisTupleIndices = m_TupleIndices [tupleSystemIndex];
    		thisTupleIndices[thisTupleIndices.Length-1] = componentIndex;

            RemoveSwapBackTupleIndex(tupleIndex);
        }

    	public void RemoveSwapBackComponent(int tupleSystemIndex, Component component)
    	{
    		int tupleIndex = m_TupleComponents[tupleSystemIndex].GetIndex(component);
    		if (tupleIndex == -1)
    			return;

    		RemoveSwapBackTupleIndex(tupleIndex);
    	}

        static int GetLightWeightIndex(GameObject go, Type type)
        {
    		//@TODO: Stop having two codepaths here... Always create light weight go?
    		var components = go.GetComponents<ComponentDataWrapperBase>();
    		foreach (var com in components)
    		{
    			if (com.m_LightWeightType == type)
    			{
                    return com.m_Index;
    			}
    		}

    		var goManager = DependencyManager.GetBehaviourManager<LightweightGameObjectManager> ();
    		var lightGO = goManager.GetLightweightGameObject (go);

    		return goManager.GetComponentIndex (lightGO, type);
        }

    	public void AddTupleIfSupported(GameObject go)
    	{
    		foreach (var componentType in m_ComponentTypes)
            {
    			if (componentType.IsSubclassOf (typeof(Component)))
    			{
    				var component = go.GetComponent (componentType);
    				if (component == null)
    					return;
    			}
    			else if (typeof(IComponentData).IsAssignableFrom (componentType))
    			{
    				int index = GetLightWeightIndex (go, componentType);
    				if (index == -1)
    					return;
    			}
            }

            for (int i = 0; i != m_ComponentTypes.Length;i++)
    		{
                var componentType = m_ComponentTypes[i];
    			if (componentType.IsSubclassOf(typeof(Component)))
                {
    				var component = go.GetComponent(componentType);
    				m_TupleComponents[i].AddComponent(component);
                }
    			else if (typeof(IComponentData).IsAssignableFrom(componentType))
                {
    				int componentIndex = GetLightWeightIndex(go, componentType);
    				m_TupleIndices[i].Add(componentIndex);
                }
    		}

    		if (m_Transforms.IsCreated)
    			m_Transforms.Add(go.transform);
    	}

    	public bool IsLightWeightTupleSupported(Type[] types)
    	{
    		if (m_Transforms.IsCreated)
    			return false;

    		foreach (var componentType in m_ComponentTypes)
    		{
    			if (System.Array.IndexOf(types, componentType) == -1)
    				return false;
    		}

    		return true;
    	}

    	public void AddTuplesUnchecked(Type componentType, int baseComponentIndex, int count)
    	{
    		int componentTypeIndex = System.Array.IndexOf (m_ComponentTypes, componentType);
    		if (componentTypeIndex == -1)
    			return;

    		for (int i = 0;i != count;i++)
    			m_TupleIndices[componentTypeIndex].Add(baseComponentIndex + i);
    	}
    }
}