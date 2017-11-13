using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
	class TupleSystem
    {
        internal struct TupleInjectionData
        {
            public FieldInfo field;
            public Type containerType;
            public Type genericType;
            public bool isReadOnly;

            internal IUpdateInjection injection;

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
                ComponentDataArray<T> array;
                
                if (tuple.isReadOnly)
                    array = group.GetReadOnlyComponentDataArray<T>();
                else
                    array = group.GetComponentDataArray<T>();
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

        TupleInjectionData[]                m_ComponentDataInjections;
        TupleInjectionData[]                m_ComponentInjections;

        ComponentGroup 						m_EntityGroup;

		internal TupleSystem(EntityManager entityManager, TupleSystem.TupleInjectionData[] componentDataInjections, TupleSystem.TupleInjectionData[] componentInjections, FieldInfo entityArrayInjection, UnityEngine.Jobs.TransformAccessArray transforms)
        {
			var requiredComponentTypes = new ComponentType[componentInjections.Length + componentDataInjections.Length];

            for (int i = 0; i != componentDataInjections.Length; i++)
				requiredComponentTypes[i] = componentDataInjections [i].genericType;
            for (int i = 0; i != componentInjections.Length; i++)
                requiredComponentTypes[i + componentDataInjections.Length] = componentInjections[i].genericType;

            m_EntityGroup = entityManager.CreateComponentGroup(transforms, requiredComponentTypes);

            m_ComponentDataInjections = componentDataInjections;
            m_ComponentInjections = componentInjections;
            m_EntityArrayInjection = entityArrayInjection;

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
            for (var i = 0; i != m_ComponentDataInjections.Length; i++)
                m_ComponentDataInjections[i].injection.UpdateInjection(targetObject, m_EntityGroup, m_ComponentDataInjections[i]);

            for (var i = 0; i != m_ComponentInjections.Length; i++)
                m_ComponentInjections[i].injection.UpdateInjection(targetObject, m_EntityGroup, m_ComponentInjections[i]);

            m_EntityGroup.UpdateTransformAccessArray();
            
            if (m_EntityArrayInjection != null)
            {
                var entityArray = m_EntityGroup.GetEntityArray();
                UnsafeUtility.SetFieldStruct(targetObject, m_EntityArrayInjection, ref entityArray);
            }
        }

    }
}
