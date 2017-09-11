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

        InjectTuples.TupleInjectionData[]   m_ComponentDataInjections;
        InjectTuples.TupleInjectionData[]   m_ComponentInjections;

        EntityGroup 						m_EntityGroup;

		internal TupleSystem(EntityManager entityManager, InjectTuples.TupleInjectionData[] componentDataInjections, InjectTuples.TupleInjectionData[] componentInjections, FieldInfo entityArrayInjection, TransformAccessArray transforms)
        {
			var requiredComponentTypes = new ComponentType[componentInjections.Length + componentDataInjections.Length];

            for (int i = 0; i != componentDataInjections.Length; i++)
				requiredComponentTypes[i] = componentDataInjections [i].genericType;
            for (int i = 0; i != componentInjections.Length; i++)
                requiredComponentTypes[i + componentDataInjections.Length] = componentInjections[i].genericType;

            if (transforms.IsCreated)
                throw new NotImplementedException("TransformAccessArray tuples are not yet supported");

            m_EntityGroup = entityManager.CreateEntityGroup(requiredComponentTypes);

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
            //@TODO:?
			//m_EntityGroup.Dispose ();
			//m_EntityGroup = null;
		}

        internal InjectTuples.TupleInjectionData[] ComponentDataInjections { get { return m_ComponentDataInjections; } }
        internal InjectTuples.TupleInjectionData[] ComponentInjections { get { return m_ComponentInjections; } }
        internal FieldInfo EntityArrayInjection { get { return m_EntityArrayInjection; } }

        //@TODO:
        public EntityArray GetEntityArray() { return m_EntityGroup.GetEntityArray(); }
		public EntityGroup EntityGroup          { get { return m_EntityGroup; } }
    }
}