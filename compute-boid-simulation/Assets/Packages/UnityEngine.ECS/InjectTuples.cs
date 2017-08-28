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
    [AttributeUsage(AttributeTargets.Field)]
    public class InjectTuples : System.Attribute
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
			public bool 		isReadOnly;

			public TupleInjectionData(FieldInfo field, Type containerType, Type genericType, bool isReadOnly)
    		{
    			this.field = field;
    			this.containerType = containerType;
    			this.genericType = genericType;
				this.isReadOnly = isReadOnly;
    		}
    	}
    		
    	internal static void UpdateInjection(TupleSystem tuples, object targetObject)
    	{
    		var dataInjections = tuples.ComponentDataInjections;
			for (var i = 0; i != dataInjections.Length; i++) 
    		{
    			object container;
				container = GetLightWeightIndexedComponents (tuples, dataInjections[i].genericType, i, false, dataInjections[i].isReadOnly);
				dataInjections[i].field.SetValue (targetObject, container);
    		}

			if (tuples.EntityArrayInjection != null)
				tuples.EntityArrayInjection.SetValue (targetObject, tuples.GetEntityArray());
    	}

		static TupleSystem CreateTuplesInjection(FieldInfo entityArray, FieldInfo transformAccessArrayField, List<TupleInjectionData> injections, List<IComponentDataManager> outReadJobDependencies, List<IComponentDataManager> outWriteJobDependencies, object targetObject)
    	{
    		var managers = new ScriptBehaviourManager[injections.Count];
    		TransformAccessArray transformAccessArray = new TransformAccessArray();
    		if (transformAccessArrayField != null)
    			transformAccessArray = new TransformAccessArray (0);

			var componentInjections = new List<TupleInjectionData>();
			var componentDataInjections = new List<TupleInjectionData>();

    		for (var i = 0; i != injections.Count; i++) 
    		{
				if (injections[i].containerType == typeof(ComponentDataArray<>))
				{
					managers[i] = DependencyManager.GetBehaviourManager (typeof(ComponentDataManager<>).MakeGenericType (injections [i].genericType));
					if (injections[i].isReadOnly)
						outReadJobDependencies.Add (managers[i] as IComponentDataManager);
					else
						outWriteJobDependencies.Add (managers[i] as IComponentDataManager);

					componentDataInjections.Add (injections [i]);
				}
				else if (injections[i].containerType == typeof(ComponentArray<>))
				{
					componentInjections.Add (injections[i]);
				}
				else
				{
					throw new System.ArgumentException ("[InjectTuples] may only be used on ComponentDataArray<>, ComponentArray<> and TransformAccessArray");
				}
    		}

			var tuples = new TupleSystem(DependencyManager.GetBehaviourManager<EntityManager>(), componentInjections.ToArray(), componentDataInjections.ToArray(), managers, entityArray, transformAccessArray);

			for (var i = 0; i != componentDataInjections.Count; i++) 
    		{
				object container = GetLightWeightIndexedComponents (tuples, componentDataInjections[i].genericType, i, true, componentDataInjections[i].isReadOnly);
				componentDataInjections[i].field.SetValue (targetObject, container);
			}

			for (var i = 0; i != componentInjections.Count; i++) 
			{
				object container = GetComponentContainer (tuples, componentInjections[i].genericType, i);
				componentInjections[i].field.SetValue (targetObject, container);
			}

    		if (transformAccessArrayField != null)
    			transformAccessArrayField.SetValue (targetObject, transformAccessArray);

    		return tuples;
    	}

		internal static bool CollectTuples(FieldInfo[] fields, ref int i, ref int activeTupleSet, out FieldInfo entityArrayField, out FieldInfo transformAccessArrayField, List<TupleInjectionData> injections)
    	{
    		transformAccessArrayField = null;
			entityArrayField = null;

    		for (; i != fields.Length;i++)
    		{
    			var field = fields[i];
    			var hasInject = field.GetCustomAttributes(typeof(InjectTuples), true).Length != 0;
    			if (hasInject)
    			{
					var isReadOnly = field.GetCustomAttributes(typeof(ReadOnlyAttribute), true).Length != 0;
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
						injections.Add (new TupleInjectionData (field, typeof(ComponentDataArray<>), field.FieldType.GetGenericArguments () [0], isReadOnly));
					}
					else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentArray<>))
					{
						if (isReadOnly)
							Debug.LogError ("[ReadOnly] may only be used with [InjectTuples] on ComponentDataArray<>");
						injections.Add (new TupleInjectionData (field, typeof(ComponentArray<>), field.FieldType.GetGenericArguments () [0], false));
					}
					else if (field.FieldType == typeof(TransformAccessArray))
					{
						if (isReadOnly)
							Debug.LogError ("[ReadOnly] may only be used with [InjectTuples] on ComponentDataArray<>");
						// Error on multiple transformAccessArray
						if (transformAccessArrayField != null)
							Debug.LogError ("[InjectTuples] may only be specified on a single TransformAccessArray");
						transformAccessArrayField = field;
					}
					else if (field.FieldType == typeof(EntityArray))
					{
						// Error on multiple transformAccessArray
						if (entityArrayField != null)
							Debug.LogError ("[InjectTuples] may only be specified on a single EntityArray");
						
						entityArrayField = field;
					}
    				else
    				{
						Debug.LogError ("[InjectTuples] may only be used on ComponentDataArray<>, ComponentArray<> or TransformAccessArray");
    					return false;
    				}
    			}
    		}

    		return true;
    	}

		static void CreateTuplesInjection(Type type, object targetObject, List<TupleSystem> outTupleSystem, List<IComponentDataManager> outReadJobDependencies, List<IComponentDataManager> outWriteJobDependencies)
    	{
    		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    		int activeTupleSetIndex = 0;
    		int fieldIndex = 0;

			FieldInfo transformAccessArrayField;
			FieldInfo entityArrayField;
    		var	injections = new List<TupleInjectionData>();

    		while (fieldIndex != fields.Length)
    		{
    			transformAccessArrayField = null;
    			injections.Clear ();

				if (!CollectTuples (fields, ref fieldIndex, ref activeTupleSetIndex, out entityArrayField, out transformAccessArrayField, injections))
    				return;

				var tupleSystem = CreateTuplesInjection (entityArrayField, transformAccessArrayField, injections, outReadJobDependencies, outWriteJobDependencies, targetObject);
    			outTupleSystem.Add (tupleSystem);
    		}
    	}

		internal static void CreateTuplesInjection(Type type, object targetObject, out TupleSystem[] outTupleSystem, out IComponentDataManager[] outReadJobDependencies, out IComponentDataManager[] outWriteJobDependencies)
    	{
    		var tuples = new List<TupleSystem> ();
			var readDependencies = new List<IComponentDataManager> ();
			var writeDependencies = new List<IComponentDataManager> ();
			CreateTuplesInjection(type, targetObject, tuples, readDependencies, writeDependencies);


			outTupleSystem = tuples.ToArray ();
			outReadJobDependencies = readDependencies.ToArray ();
			outWriteJobDependencies = writeDependencies.ToArray ();
    	}


		static object GetLightWeightIndexedComponents(TupleSystem tuple, Type type, int index, bool create, bool readOnly)
		{
			object[] args = { index, create, readOnly };
			return tuple.GetType ().GetMethod ("GetLightWeightIndexedComponents").MakeGenericMethod (type).Invoke(tuple, args);
		}

    	static object GetComponentContainer(TupleSystem tuple, Type type, int index)
    	{
    		object[] args = { index };
    		return tuple.GetType ().GetMethod ("GetComponentContainer").MakeGenericMethod (type).Invoke(tuple, args);
    	}
    }
}