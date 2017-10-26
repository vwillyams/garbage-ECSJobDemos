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
				container = GetComponentDataArray (tuples, dataInjections[i].genericType, dataInjections[i].isReadOnly);
				dataInjections[i].field.SetValue (targetObject, container);
    		}

            var componentInjections = tuples.ComponentInjections;
            for (var i = 0; i != componentInjections.Length; i++)
            {
                object container;
                container = GetComponentArray(tuples, componentInjections[i].genericType);
                componentInjections[i].field.SetValue(targetObject, container);
            }

			tuples.UpdateTransformAccessArray();
            if (tuples.EntityArrayInjection != null)
				tuples.EntityArrayInjection.SetValue (targetObject, tuples.GetEntityArray());
    	}

        static TupleSystem CreateTuplesInjection(FieldInfo entityArrayField, FieldInfo transformAccessArrayField, List<TupleInjectionData> injections, List<ComponentType> outReadJobDependencies, List<ComponentType> outWriteJobDependencies, object targetObject)
    	{
			var componentInjections = new List<TupleInjectionData>();
			var componentDataInjections = new List<TupleInjectionData>();

    		for (var i = 0; i != injections.Count; i++) 
    		{
				if (injections[i].containerType == typeof(ComponentDataArray<>))
				{
					if (injections[i].isReadOnly)
                        outReadJobDependencies.Add (new ComponentType(injections[i].genericType));
					else
                        outWriteJobDependencies.Add (new ComponentType(injections[i].genericType));

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

			var transforms = new TransformAccessArray();
			if (transformAccessArrayField != null)
			{
				transforms = new TransformAccessArray(0);
				transformAccessArrayField.SetValue (targetObject, transforms);
			}
            var tuples = new TupleSystem(DependencyManager.GetBehaviourManager<EntityManager>(), componentDataInjections.ToArray(), componentInjections.ToArray(), entityArrayField, transforms);

			UpdateInjection(tuples, targetObject);
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
						if (tupleSetIndex < activeTupleSet)
							Debug.LogError ("[InjectTuples] must be ordered incrementally by their index");
						
    					activeTupleSet = tupleSetIndex;

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

		static void CreateTuplesInjection(Type type, object targetObject, List<TupleSystem> outTupleSystem, List<ComponentType> outReadJobDependencies, List<ComponentType> outWriteJobDependencies)
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

        internal static void CreateTuplesInjection(Type type, object targetObject, out TupleSystem[] outTupleSystem, out ComponentType[] outReadJobDependencies, out ComponentType[] outWriteJobDependencies)
    	{
    		var tuples = new List<TupleSystem> ();
            var readDependencies = new List<ComponentType> ();
			var writeDependencies = new List<ComponentType> ();
			CreateTuplesInjection(type, targetObject, tuples, readDependencies, writeDependencies);


			outTupleSystem = tuples.ToArray ();
			outReadJobDependencies = readDependencies.ToArray ();
			outWriteJobDependencies = writeDependencies.ToArray ();
    	}


		static object GetComponentDataArray(TupleSystem tuple, Type type, bool readOnly)
		{
			object[] args = { readOnly };
			return tuple.GetType ().GetMethod ("GetComponentDataArray").MakeGenericMethod (type).Invoke(tuple, args);
		}

    	static object GetComponentArray(TupleSystem tuple, Type type)
    	{
    		return tuple.GetType ().GetMethod ("GetComponentArray").MakeGenericMethod (type).Invoke(tuple, null);
    	}
    }
}