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
		FieldInfo 							m_EntityArrayInjection;

		InjectTuples.TupleInjectionData[]	m_ComponentDataInjections;

		EntityGroup 						m_EntityGroup;

		internal TupleSystem(EntityManager entityManager, InjectTuples.TupleInjectionData[] componentDataInjections, ScriptBehaviourManager[] componentDataManagers, InjectTuples.TupleInjectionData[] componentInjections, FieldInfo entityArrayInjection, TransformAccessArray transforms)
        {
			var componentTypes = new Type[componentInjections.Length];
			var componentDataTypes = new Type[componentDataInjections.Length];
			for (int i = 0; i != componentInjections.Length; i++)
				componentTypes[i] = componentInjections[i].genericType;
			for (int i = 0; i != componentDataInjections.Length; i++)
				componentDataTypes[i] = componentDataInjections [i].genericType;
			
			m_EntityGroup = new EntityGroup (entityManager, componentDataTypes, componentDataManagers, componentTypes, transforms);

			m_ComponentDataInjections = componentDataInjections;
			m_EntityArrayInjection = entityArrayInjection;
        }

		public ComponentDataArray<T> GetComponentDataArray<T>(int index, bool readOnly) where T : struct, IComponentData
		{
			return m_EntityGroup.GetComponentDataArray<T>(index, readOnly);
		}
			
		public ComponentArray<T> GetComponentArray<T>(int index) where T : Component
		{
			return m_EntityGroup.GetComponentArray<T> (index);
		}


		public void Dispose()
		{
			m_EntityGroup.Dispose ();
			m_EntityGroup = null;
		}

		internal InjectTuples.TupleInjectionData[] ComponentDataInjections { get { return m_ComponentDataInjections; } }
		internal FieldInfo EntityArrayInjection { get { return m_EntityArrayInjection; } }

		public EntityArray GetEntityArray()     { return m_EntityGroup.GetEntityArray (); }
		public EntityGroup EntityGroup          { get { return m_EntityGroup; } }

    }
}