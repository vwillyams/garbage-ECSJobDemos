using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;
using System.Collections.ObjectModel;

namespace UnityEngine.ECS
{
	//@TODO: Public ness etc
	public class TupleSystem
    {
        internal struct TupleInjectionData
        {
            public FieldInfo field;
            public Type containerType;
            public Type genericType;
            public bool isReadOnly;

            public TupleInjectionData(FieldInfo field, Type containerType, Type genericType, bool isReadOnly)
            {
                this.field = field;
                this.containerType = containerType;
                this.genericType = genericType;
                this.isReadOnly = isReadOnly;
            }
        }

		FieldInfo 							m_EntityArrayInjection;

        TupleInjectionData[]                m_ComponentDataInjections;
        TupleInjectionData[]    m_ComponentInjections;

        EntityGroup 						m_EntityGroup;

		internal TupleSystem(EntityManager entityManager, TupleSystem.TupleInjectionData[] componentDataInjections, TupleSystem.TupleInjectionData[] componentInjections, FieldInfo entityArrayInjection, TransformAccessArray transforms)
        {
			var requiredComponentTypes = new ComponentType[componentInjections.Length + componentDataInjections.Length];

            for (int i = 0; i != componentDataInjections.Length; i++)
				requiredComponentTypes[i] = componentDataInjections [i].genericType;
            for (int i = 0; i != componentInjections.Length; i++)
                requiredComponentTypes[i + componentDataInjections.Length] = componentInjections[i].genericType;

            m_EntityGroup = entityManager.CreateEntityGroup(transforms, requiredComponentTypes);

            m_ComponentDataInjections = componentDataInjections;
            m_ComponentInjections = componentInjections;
            m_EntityArrayInjection = entityArrayInjection;
        }

		public ComponentDataArray<T> GetComponentDataArray<T>(bool readOnly) where T : struct, IComponentData
		{
			return m_EntityGroup.GetComponentDataArray<T>(readOnly);
		}

		public ComponentArray<T> GetComponentArray<T>() where T : Component
		{
            return m_EntityGroup.GetComponentArray<T>();
        }

		public void Dispose()
		{
			m_EntityGroup.Dispose ();
			m_EntityGroup = null;
		}

        //@TODO:
        public void UpdateTransformAccessArray()
        {
            m_EntityGroup.UpdateTransformAccessArray();
        }
        public EntityArray GetEntityArray() { return m_EntityGroup.GetEntityArray(); }
		public EntityGroup EntityGroup          { get { return m_EntityGroup; } }


        object GetComponentDataArray(Type type, bool readOnly)
        {
            object[] args = { readOnly };
            return GetType().GetMethod("GetComponentDataArray").MakeGenericMethod(type).Invoke(this, args);
        }

        object GetComponentArray(Type type)
        {
            return GetType().GetMethod("GetComponentArray").MakeGenericMethod(type).Invoke(this, null);
        }

        public void UpdateInjection(object targetObject)
        {
            for (var i = 0; i != m_ComponentDataInjections.Length; i++)
            {
                object container;
                container = GetComponentDataArray(m_ComponentDataInjections[i].genericType, m_ComponentDataInjections[i].isReadOnly);
                m_ComponentDataInjections[i].field.SetValue(targetObject, container);
            }

            for (var i = 0; i != m_ComponentInjections.Length; i++)
            {
                object container;
                container = GetComponentArray(m_ComponentInjections[i].genericType);
                m_ComponentInjections[i].field.SetValue(targetObject, container);
            }

            UpdateTransformAccessArray();
            if (m_EntityArrayInjection != null)
                m_EntityArrayInjection.SetValue(targetObject, GetEntityArray());
        }

    }
}