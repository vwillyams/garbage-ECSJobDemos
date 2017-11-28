﻿using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class InjectComponentGroupAttribute : System.Attribute
	{

	}
	
	class InjectComponentGroupData
    {
	    FieldInfo 			m_EntityArrayInjection;
	    FieldInfo 			m_TransformAccessArrayInjections;
	    FieldInfo 			m_LengthInjection;

	    InjectionData[]     m_ComponentInjections;

	    ComponentGroup 		m_EntityGroup;

	    FieldInfo 			m_GroupField;
	    

        class UpdateInjectionComponentDataArray<T> : IUpdateInjection where T : struct, IComponentData
        {
            public void UpdateInjection(object targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
            {
                var array = group.GetComponentDataArray<T>();
                UnsafeUtility.SetFieldStruct(targetObject, injection.field, ref array);
            }
        }

	    class UpdateInjectionComponentDataFixedArray<T> : IUpdateInjection where T : struct
	    {
		    public void UpdateInjection(object targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
		    {
			    var array = group.GetFixedArrayArray<T>();
			    UnsafeUtility.SetFieldStruct(targetObject, injection.field, ref array);
		    }
	    }
	    
        class UpdateInjectionComponentArray<T> : IUpdateInjection where T : UnityEngine.Component
        {
            public void UpdateInjection(object targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
            {
                var array = group.GetComponentArray<T>();
                UnsafeUtility.SetFieldStruct(targetObject, injection.field, ref array);
            }
        }

		InjectComponentGroupData(EntityManager entityManager, FieldInfo groupField, InjectionData[] componentInjections, FieldInfo entityArrayInjection, FieldInfo transformAccessArrayInjection, FieldInfo lengthInjection)
		{
            var transformsCount = transformAccessArrayInjection != null ? 1 : 0;
			var requiredComponentTypes = new ComponentType[componentInjections.Length + transformsCount];

            for (int i = 0; i != componentInjections.Length; i++)
                requiredComponentTypes[i] = new ComponentType(componentInjections[i].genericType, componentInjections[i].isReadOnly ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite);
		    
		    if (transformsCount != 0)
		        requiredComponentTypes[componentInjections.Length] = typeof(Transform);

            m_EntityGroup = entityManager.CreateComponentGroup(requiredComponentTypes);

            m_ComponentInjections = componentInjections;
			m_EntityArrayInjection = entityArrayInjection;
			m_LengthInjection = lengthInjection;
			m_TransformAccessArrayInjections = transformAccessArrayInjection;

			m_GroupField = groupField;

        }

		public void Dispose()
		{
			m_EntityGroup.Dispose ();
			m_EntityGroup = null;
		}

		public ComponentGroup EntityGroup          { get { return m_EntityGroup; } }

        public void UpdateInjection(object targetObject)
        {
	        //@TODO: Fix GC alloc
            object groupObject = Activator.CreateInstance(m_GroupField.FieldType);

            for (var i = 0; i != m_ComponentInjections.Length; i++)
                m_ComponentInjections[i].injection.UpdateInjection(groupObject, null, m_EntityGroup, m_ComponentInjections[i]);

	        if (m_TransformAccessArrayInjections != null)
	        {
		        var transformsArray = m_EntityGroup.GetTransformAccessArray();
		        UnsafeUtility.SetFieldStruct(groupObject, m_TransformAccessArrayInjections, ref transformsArray);
	        }
	        
            if (m_EntityArrayInjection != null)
            {
                var entityArray = m_EntityGroup.GetEntityArray();
                UnsafeUtility.SetFieldStruct(groupObject, m_EntityArrayInjection, ref entityArray);
            }

	        if (m_LengthInjection != null)
	        {
		        int length = m_EntityGroup.Length;
		        UnsafeUtility.SetFieldStruct(groupObject, m_LengthInjection, ref length);
	        }

	        m_GroupField.SetValue(targetObject, groupObject);
        }

	    static public InjectComponentGroupData CreateInjection(Type injectedGroupType, FieldInfo groupField, EntityManager entityManager)
	    {
		    FieldInfo entityArrayField;
		    FieldInfo transformAccessArrayField;
		    FieldInfo lengthField;
		    var componentInjections = new List<InjectionData>();
		    var error = CollectInjectedGroup(injectedGroupType, out entityArrayField, out transformAccessArrayField, out lengthField, componentInjections);
		    if (error != null)
			    throw new System.InvalidOperationException(error);

		    return new InjectComponentGroupData(entityManager, groupField, componentInjections.ToArray(), entityArrayField, transformAccessArrayField, lengthField);
	    }

	    static string CollectInjectedGroup(Type injectedGroupType, out FieldInfo entityArrayField, out FieldInfo transformAccessArrayField, out FieldInfo lengthField, List<InjectionData> componentInjections)
	    {
			//@TODO: Improved error messages... should include full struct pathname etc.
		    
		    var fields = injectedGroupType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		    transformAccessArrayField = null;
		    entityArrayField = null;
		    lengthField = null;

			foreach(var field in fields)
    		{
				var isReadOnly = field.GetCustomAttributes(typeof(ReadOnlyAttribute), true).Length != 0;

			    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentDataArray<>))
			    {
				    var injection = new InjectionData(field, typeof(ComponentDataArray<>), field.FieldType.GetGenericArguments()[0], isReadOnly);
					
				    var injectionType = typeof(UpdateInjectionComponentDataArray<>).MakeGenericType(injection.genericType);
				    injection.injection = (IUpdateInjection)Activator.CreateInstance(injectionType);

				    componentInjections.Add (injection);
			    }
			    else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(FixedArrayArray<>))
			    {
				    var injection = new InjectionData(field, typeof(FixedArrayArray<>), field.FieldType.GetGenericArguments()[0], isReadOnly);
					
				    var injectionType = typeof(UpdateInjectionComponentDataFixedArray<>).MakeGenericType(injection.genericType);
				    injection.injection = (IUpdateInjection)Activator.CreateInstance(injectionType);

				    componentInjections.Add (injection);
			    }
				else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentArray<>))
				{
					if (isReadOnly)
						return "[ReadOnly] may not be used on ComponentArray<>, it can only be used on ComponentDataArray<>";

					var injection = new InjectionData(field, typeof(ComponentArray<>), field.FieldType.GetGenericArguments()[0], false);
					
					var injectionType = typeof(UpdateInjectionComponentArray<>).MakeGenericType(injection.genericType);
					injection.injection = (IUpdateInjection)Activator.CreateInstance(injectionType);
					
					componentInjections.Add (injection);
				}
				else if (field.FieldType == typeof(TransformAccessArray))
				{
					if (isReadOnly)
						return "[ReadOnly] may not be used on a TransformAccessArray only on ComponentDataArray<>";
					// Error on multiple transformAccessArray
					if (transformAccessArrayField != null)
						return "A [InjectComponentGroup] struct, may only contain a single TransformAccessArray";
					transformAccessArrayField = field;
				}
				else if (field.FieldType == typeof(EntityArray))
				{
					// Error on multiple EntityArray
					if (entityArrayField != null)
						return "A [InjectComponentGroup] struct, may only contain a single EntityArray";
					
					entityArrayField = field;
				}
				else if (field.FieldType == typeof(int))
			    {
				    // Error on multiple EntityArray
				    if (field.Name != "Length")
					    return "A [InjectComponentGroup] struct, supports only a specialized int storing the length of the group. (\"int Length;\")";
				    lengthField = field;
			    }
			    else
			    {
				    return
					    "[InjectComponentGroup] may only be used on ComponentDataArray<>, ComponentArray<> or TransformAccessArray";
			    }
		    }

		    return null;
	    }

    }
}
