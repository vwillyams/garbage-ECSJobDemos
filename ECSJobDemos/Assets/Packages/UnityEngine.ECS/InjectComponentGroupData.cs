using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
	class TupleSystem
    {
        internal struct TupleInjectionData
        {
            public FieldInfo           field;
            public Type                containerType;
            public Type                genericType;
            public bool                isReadOnly;

            internal IUpdateInjection  injection;

            public TupleInjectionData(FieldInfo field, Type containerType, Type genericType, bool isReadOnly)
            {
                this.field = field;
                this.containerType = containerType;
                this.genericType = genericType;
                this.isReadOnly = isReadOnly;
                this.injection = null;
            }
        }

        internal  interface IUpdateInjection
        {
            void UpdateInjection(object targetObject, ComponentGroup group, TupleInjectionData tuple);
        }

        internal class UpdateInjectionComponentDataArray<T> : IUpdateInjection where T : struct, IComponentData
        {
            public void UpdateInjection(object targetObject, ComponentGroup group, TupleInjectionData tuple)
            {
                var array = group.GetComponentDataArray<T>();
                UnsafeUtility.SetFieldStruct(targetObject, tuple.field, ref array);
            }
        }

        internal class UpdateInjectionComponentArray<T> : IUpdateInjection where T : UnityEngine.Component
        {
            public void UpdateInjection(object targetObject, ComponentGroup group, TupleInjectionData tuple)
            {
                var array = group.GetComponentArray<T>();
                UnsafeUtility.SetFieldStruct(targetObject, tuple.field, ref array);
            }
        }

	    FieldInfo 							m_EntityArrayInjection;
	    FieldInfo 							m_TransformAccessArrayInjections;
	    FieldInfo 							m_LengthInjection;

        TupleInjectionData[]                m_ComponentDataInjections;
        TupleInjectionData[]                m_ComponentInjections;

        ComponentGroup 						m_EntityGroup;

        FieldInfo 							m_GroupField;
        

	    
		internal TupleSystem(EntityManager entityManager, FieldInfo groupField, TupleSystem.TupleInjectionData[] componentDataInjections, TupleSystem.TupleInjectionData[] componentInjections, FieldInfo entityArrayInjection, FieldInfo transformAccessArrayInjection, FieldInfo lengthInjection)
		{
            var transformsCount = transformAccessArrayInjection != null ? 1 : 0;
			var requiredComponentTypes = new ComponentType[componentInjections.Length + componentDataInjections.Length + transformsCount];

            for (int i = 0; i != componentDataInjections.Length; i++)
                requiredComponentTypes[i] = new ComponentType(componentDataInjections[i].genericType, componentDataInjections[i].isReadOnly);
				
            for (int i = 0; i != componentInjections.Length; i++)
                requiredComponentTypes[i + componentDataInjections.Length] = componentInjections[i].genericType;
		    
		    if (transformsCount != 0)
		        requiredComponentTypes[componentInjections.Length + componentDataInjections.Length] = typeof(Transform);

            m_EntityGroup = entityManager.CreateComponentGroup(requiredComponentTypes);

            m_ComponentDataInjections = componentDataInjections;
            m_ComponentInjections = componentInjections;
			m_EntityArrayInjection = entityArrayInjection;
			m_LengthInjection = lengthInjection;
			m_TransformAccessArrayInjections = transformAccessArrayInjection;

			m_GroupField = groupField;

            for (int i = 0; i != m_ComponentDataInjections.Length;i++)
            {
                var injectionType = typeof(UpdateInjectionComponentDataArray<>).MakeGenericType(m_ComponentDataInjections[i].genericType);
                m_ComponentDataInjections[i].injection = (IUpdateInjection)Activator.CreateInstance(injectionType);
            }

            for (int i = 0; i != m_ComponentInjections.Length; i++)
            {
                var injectionType = typeof(UpdateInjectionComponentArray<>).MakeGenericType(m_ComponentInjections[i].genericType);
                m_ComponentInjections[i].injection = (IUpdateInjection)Activator.CreateInstance(injectionType);
            }
        }

		public void Dispose()
		{
			m_EntityGroup.Dispose ();
			m_EntityGroup = null;
		}

		public ComponentGroup EntityGroup          { get { return m_EntityGroup; } }

        public void UpdateInjection(object targetObject)
        {
            object groupObject = Activator.CreateInstance(m_GroupField.FieldType);

            for (var i = 0; i != m_ComponentDataInjections.Length; i++)
                m_ComponentDataInjections[i].injection.UpdateInjection(groupObject, m_EntityGroup, m_ComponentDataInjections[i]);

            for (var i = 0; i != m_ComponentInjections.Length; i++)
                m_ComponentInjections[i].injection.UpdateInjection(groupObject, m_EntityGroup, m_ComponentInjections[i]);

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

	    static public TupleSystem CreateTupleSystem(Type injectedGroupType, FieldInfo groupField, EntityManager entityManager)
	    {
		    FieldInfo entityArrayField;
		    FieldInfo transformAccessArrayField;
		    FieldInfo lengthField;
		    var componentDataInjections = new List<TupleSystem.TupleInjectionData>();
		    var componentInjections = new List<TupleSystem.TupleInjectionData>();
		    var error = CollectInjectedGroup(injectedGroupType, out entityArrayField, out transformAccessArrayField, out lengthField, componentDataInjections, componentInjections);
		    if (error != null)
		    {
			    //@TODO: Throw expceptions in case of error?
			    Debug.LogError(error);
			    return null;
		    }

		    return new TupleSystem(entityManager, groupField, componentDataInjections.ToArray(), componentInjections.ToArray(), entityArrayField, transformAccessArrayField, lengthField);
	    }

	    static public TupleSystem[] InjectComponentGroups(Type componentSystemType, EntityManager entityManager)
	    {
		    var fields = componentSystemType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		    var tuples = new List<TupleSystem>();
		    foreach (var field in fields)
		    {
			    var attr = field.GetCustomAttributes(typeof(InjectComponentGroupAttribute), true);

			    if (attr.Length != 0)
				    tuples.Add(CreateTupleSystem(field.FieldType, field, entityManager));
		    }

		    return tuples.ToArray();
	    }

	    static string CollectInjectedGroup(Type injectedGroupType, out FieldInfo entityArrayField, out FieldInfo transformAccessArrayField, out FieldInfo lengthField, List<TupleSystem.TupleInjectionData> componentDataInjections, List<TupleSystem.TupleInjectionData> componentInjections)
	    {
			//@TODO: Improved error messages... should include full struct pathname etc.
		    
		    var fields = injectedGroupType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		    var	injections = new List<TupleSystem.TupleInjectionData>();
		    transformAccessArrayField = null;
		    entityArrayField = null;
		    lengthField = null;

			foreach(var field in fields)
    		{
				var isReadOnly = field.GetCustomAttributes(typeof(ReadOnlyAttribute), true).Length != 0;

				if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentDataArray<>))
				{
					componentDataInjections.Add (new TupleSystem.TupleInjectionData (field, typeof(ComponentDataArray<>), field.FieldType.GetGenericArguments () [0], isReadOnly));
				}
				else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(ComponentArray<>))
				{
					if (isReadOnly)
						return "[ReadOnly] may not be used on ComponentArray<>, it can only be used on ComponentDataArray<>";
					componentInjections.Add (new TupleSystem.TupleInjectionData (field, typeof(ComponentArray<>), field.FieldType.GetGenericArguments () [0], false));
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
