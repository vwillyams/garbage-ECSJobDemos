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
		//@TODO: Internalize by moving into game object manager instead...
    	public class RegisteredTuple
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
		IGenericComponentListInjection[]    m_TupleComponentInjections;

		Type[]                      		m_ComponentTypes;
		Type[]                      		m_ComponentDataTypes;
		InjectTuples.TupleInjectionData[]	m_ComponentDataInjections;
    	ScriptBehaviourManager[]    		m_LightWeightManagers;
		LightweightGameObjectManager 		m_GameObjectManager;

		internal TupleSystem(LightweightGameObjectManager gameObjectManager, InjectTuples.TupleInjectionData[] componentInjections, InjectTuples.TupleInjectionData[] componentDataInjections, ScriptBehaviourManager[] lightweightManagers, TransformAccessArray transforms)
        {
			this.m_GameObjectManager = gameObjectManager;
			this.m_ComponentTypes = new Type[componentInjections.Length];
			this.m_ComponentDataTypes = new Type[componentDataInjections.Length];
    		this.m_LightWeightManagers = lightweightManagers;
    		this.m_Transforms = transforms;
			m_ComponentDataInjections = componentDataInjections;

			m_TupleComponentInjections = new IGenericComponentListInjection[componentInjections.Length];
			m_TupleIndices = new NativeList<int>[componentDataInjections.Length];

			for (int i = 0; i != componentInjections.Length; i++)
			{
				var componentType = componentInjections[i].genericType;

				var listType = typeof(GenericComponentListInjection<>).MakeGenericType (new Type[] { componentType });
				m_TupleComponentInjections [i] = (IGenericComponentListInjection)Activator.CreateInstance (listType);
				m_ComponentTypes[i] = componentType;
			}

			for (int i = 0; i != componentDataInjections.Length; i++)
			{
				m_TupleIndices[i] = new NativeList<int>(0, Allocator.Persistent);
				m_ComponentDataTypes[i] = componentDataInjections [i].genericType;
			}
        }

    	public void Dispose()
    	{
    		for (int i = 0; i != m_TupleIndices.Length; i++)
    		{
    			if (m_TupleIndices[i].IsCreated)
    				m_TupleIndices[i].Dispose();
    		}

    		//@TODO: Shouldn't dispose check this itself???
    		if (m_Transforms.IsCreated)
    			m_Transforms.Dispose ();
    	}
			
    	public ComponentDataArray<T> GetLightWeightIndexedComponents<T>(int index, bool create) where T : struct, IComponentData
        {
    		var manager = m_LightWeightManagers[index] as LightweightComponentManager<T>;
    		var container = new ComponentDataArray<T> (manager.m_Data, m_TupleIndices[index]);

    		if (create)
    			manager.m_RegisteredTuples.Add(new RegisteredTuple(this, index));

            return container;
        }

		internal InjectTuples.TupleInjectionData[] ComponentDataInjections { get { return m_ComponentDataInjections; } }

        public ComponentArray<T> GetComponentContainer<T>(int index) where T : Component
    	{
			ComponentArray<T> array;
			array.m_List = (List<T>)m_TupleComponentInjections[index];
			return array;
    	}

    	private void RemoveSwapBackTupleIndex(int tupleIndex)
        {
            for (int i = 0; i != m_TupleComponentInjections.Length; i++)
				m_TupleComponentInjections [i].RemoveAtSwapBackComponent (tupleIndex);

			for (int i = 0; i != m_TupleIndices.Length; i++)
				m_TupleIndices[i].RemoveAtSwapBack (tupleIndex);
			
    		if (m_Transforms.IsCreated)
    	        m_Transforms.RemoveAtSwapBack(tupleIndex);
    	}

    	public void RemoveSwapBackLightWeightComponent(int tupleSystemIndex, int componentIndex)
        {
            int tupleIndex = -1;
    		for (int i = 0; i != m_TupleIndices[tupleSystemIndex].Length; i++)
            {
				if (m_TupleIndices [tupleSystemIndex] [i] == componentIndex)
				{
					tupleIndex = i;
					break;
				}
            }

    		if (tupleIndex == -1)
    			return;

            RemoveSwapBackTupleIndex(tupleIndex);
        }

    	public void RemoveSwapBackComponent(int tupleSystemIndex, Component component)
    	{
    		int tupleIndex = m_TupleComponentInjections[tupleSystemIndex].GetIndex(component);
    		if (tupleIndex == -1)
    			return;

    		RemoveSwapBackTupleIndex(tupleIndex);
    	}

		bool IsTupleSupported(GameObject go, LightweightGameObject lightGameObject)
		{
			foreach (var componentType in m_ComponentTypes)
			{
				var component = go.GetComponent (componentType);
				if (component == null)
					return false;
			}

			foreach (var componentType in m_ComponentDataTypes)
			{
				//@TODO: use componentTypeIndex...
				if (!m_GameObjectManager.HasComponent (lightGameObject, componentType))
					return false;
			}

			if (m_Transforms.IsCreated && go == null)
				return false;

			return true;
		}
			
    	public void AddTupleIfSupported(GameObject go, LightweightGameObject lightGameObject)
    	{
			if (!IsTupleSupported (go, lightGameObject))
				return;

			// Component injections
			for (int i = 0; i != m_ComponentTypes.Length; i++)
			{
				var component = go.GetComponent (m_ComponentTypes[i]);
				m_TupleComponentInjections[i].AddComponent (component);
			}

			// Lightweight component injections
			for (int i = 0; i != m_ComponentDataTypes.Length; i++)
			{		
				int componentIndex = m_GameObjectManager.GetComponentIndex(lightGameObject, m_ComponentDataTypes[i]);
				Assert.AreNotEqual (-1, componentIndex);

    			m_TupleIndices[i].Add(componentIndex);
    		}

			// Transform component injections
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

		//@TODO: Rename to lightweight
		public void AddTuplesUnchecked(Type componentType, NativeSlice<int> componentIndices)
    	{
    		int componentTypeIndex = System.Array.IndexOf (m_ComponentDataTypes, componentType);
    		if (componentTypeIndex == -1)
    			return;

			var tuplesIndices = m_TupleIndices[componentTypeIndex];

			int count = componentIndices.Length;
			tuplesIndices.ResizeUninitialized (tuplesIndices.Length + count);
			var indices = new NativeSlice<int> (tuplesIndices, tuplesIndices.Length - count);
    		for (int i = 0;i != count;i++)
				indices[i] = componentIndices[i];
    	}
    }
}