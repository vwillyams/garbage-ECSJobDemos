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

    [AttributeUsage(AttributeTargets.Field)]
    class InjectTuples : System.Attribute
    {
    	public int TupleSetIndex { get; set; }

    	public InjectTuples(int tupleSetIndex = 0)
    	{
    		TupleSetIndex = tupleSetIndex;
    	}

    	//@TODO: Capacity defaulting mechanism...

    	internal struct TupleInjectionData
    	{
			public FieldInfo 	field;
    		public Type 		containerType;
    		public Type 		genericType;

    		public TupleInjectionData(FieldInfo field, Type containerType, Type genericType)
    		{
    			this.field = field;
    			this.containerType = containerType;
    			this.genericType = genericType;
    		}
    	}
    		
    	internal static void UpdateInjection(TupleSystem tuples, object targetObject)
    	{
    		var injections = tuples.InjectionData;
    		for (var i = 0; i != injections.Length; i++) 
    		{
    			var field = injections[i].field;
    			object container;

				if (injections [i].containerType == typeof(ComponentDataArray<>))
				{
					container = GetLightWeightIndexedComponents (tuples, injections [i].genericType, i, false);
					field.SetValue (targetObject, container);
				}
    		}
    	}

    	static TupleSystem CreateTuplesInjection(FieldInfo transformAccessArrayField, List<TupleInjectionData> injections, List<ILightweightComponentManager> outJobDependencies, object targetObject)
    	{
    		//@TODO: Separate injection from collecting injections...

    		var types = new Type[injections.Count];
    		var managers = new ScriptBehaviourManager[injections.Count];
    		TransformAccessArray transformAccessArray = new TransformAccessArray();
    		if (transformAccessArrayField != null)
    			transformAccessArray = new TransformAccessArray (0);

    		for (var i = 0; i != injections.Count; i++) 
    		{
    			types[i] = injections[i].genericType;

    			if (injections [i].containerType == typeof(ComponentDataArray<>))
    			{
    				managers[i] = DependencyManager.GetBehaviourManager (typeof(LightweightComponentManager<>).MakeGenericType (injections [i].genericType));
    				outJobDependencies.Add (managers[i] as ILightweightComponentManager);
    			}
    		}

    		var tuples = new TupleSystem(types, managers, injections.ToArray(), transformAccessArray);

    		for (var i = 0; i != injections.Count; i++) 
    		{
    			var field = injections[i].field;
    			object container;
    			if (injections [i].containerType == typeof(ComponentArray<>))
    				container = GetComponentContainer (tuples, types[i], i);
    			else if (injections [i].containerType == typeof(ComponentDataArray<>))
    				container = GetLightWeightIndexedComponents (tuples, injections[i].genericType, i, true);
    			else
    				throw new System.InvalidOperationException();

    			field.SetValue (targetObject, container);
    		}

    		if (transformAccessArrayField != null)
    			transformAccessArrayField.SetValue (targetObject, transformAccessArray);

    		return tuples;
    	}

    	internal static bool CollectTuples(FieldInfo[] fields, ref int i, ref int activeTupleSet, out FieldInfo transformAccessArrayField, List<TupleInjectionData> injections)
    	{
    		transformAccessArrayField = null;

    		for (; i != fields.Length;i++)
    		{
    			var field = fields[i];
    			var hasInject = field.GetCustomAttributes(typeof(InjectTuples), true).Length != 0;
    			if (hasInject)
    			{
    				InjectTuples src = (InjectTuples)field.GetCustomAttributes (typeof(InjectTuples), true)[0];
    				int tupleSetIndex = src.TupleSetIndex;
    				if (activeTupleSet != tupleSetIndex)
    				{
    					activeTupleSet = tupleSetIndex;

    					//@TODO: Must be increasing tupleset index...

    					return true;
    				}

    				if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentDataArray<>))
    				{
    					injections.Add (new TupleInjectionData(field, typeof(ComponentDataArray<>), field.FieldType.GetGenericArguments()[0]));
    				} 
    				else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentArray<>))
    				{
						injections.Add (new TupleInjectionData(field, typeof(ComponentArray<>), field.FieldType.GetGenericArguments()[0]));
    				}
    				else if (field.FieldType == typeof(TransformAccessArray))
    				{
    					//@TODO: Error on multiple transformAccessArray
    					transformAccessArrayField = field;
    				}
    				else
    				{
    					//@TODO: Error or exception...
    					return false;
    				}
    			}
    		}

    		return true;
    	}

    	static void CreateTuplesInjection(Type type, object targetObject, List<TupleSystem> outTupleSystem, List<ILightweightComponentManager> outJobDependencies)
    	{
    		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    		int activeTupleSetIndex = 0;
    		int fieldIndex = 0;

    		FieldInfo transformAccessArrayField;
    		var	injections = new List<TupleInjectionData>();

    		while (fieldIndex != fields.Length)
    		{
    			transformAccessArrayField = null;
    			injections.Clear ();

    			if (!CollectTuples (fields, ref fieldIndex, ref activeTupleSetIndex, out transformAccessArrayField, injections))
    				return;

    			var tupleSystem = CreateTuplesInjection (transformAccessArrayField, injections, outJobDependencies, targetObject);
    			outTupleSystem.Add (tupleSystem);
    		}
    	}

    	internal static void CreateTuplesInjection(Type type, object targetObject, out TupleSystem[] outTupleSystem, out ILightweightComponentManager[] outJobDependencies)
    	{
    		var tuples = new List<TupleSystem> ();
    		var dependencies = new List<ILightweightComponentManager> ();
    		CreateTuplesInjection(type, targetObject, tuples, dependencies);


    		outTupleSystem = tuples.ToArray ();
    		outJobDependencies = dependencies.ToArray ();
    	}


    	static object GetLightWeightIndexedComponents(TupleSystem tuple, Type type, int index, bool create)
    	{
    		object[] args = { index, create };
    		return tuple.GetType ().GetMethod ("GetLightWeightIndexedComponents").MakeGenericMethod (type).Invoke(tuple, args);
    	}

    	static object GetComponentContainer(TupleSystem tuple, Type type, int index)
    	{
    		object[] args = { index };
    		return tuple.GetType ().GetMethod ("GetComponentContainer").MakeGenericMethod (type).Invoke(tuple, args);
    	}
    }
}