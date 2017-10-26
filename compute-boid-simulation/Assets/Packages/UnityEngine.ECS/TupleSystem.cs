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
                var array = group.GetComponentDataArray<T>(tuple.isReadOnly);
                UnsafeUtility.SetFieldStruct(targetObject, tuple.field, ref array);
            }
        }

        internal class UpdateInjectionComponentArray<T> : IUpdateInjection where T : UnityEngine.Component
        {
            public void UpdateInjection(object targetObject, ComponentGroup group, TupleInjectionData tuple)
            {
                var array = group.GetComponentArray<T>();
                //@TODO: Make sure SetFieldStruct works with managed types
                //UnsafeUtility.SetFieldStruct(targetObject, tuple.field, ref array);
                tuple.field.SetValue(targetObject, array);
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
		public ComponentGroup EntityGroup          { get { return m_EntityGroup; } }


        public void UpdateInjection(object targetObject)
        {
            for (var i = 0; i != m_ComponentDataInjections.Length; i++)
                m_ComponentDataInjections[i].injection.UpdateInjection(targetObject, m_EntityGroup, m_ComponentDataInjections[i]);

            for (var i = 0; i != m_ComponentInjections.Length; i++)
                m_ComponentInjections[i].injection.UpdateInjection(targetObject, m_EntityGroup, m_ComponentInjections[i]);

            UpdateTransformAccessArray();
            if (m_EntityArrayInjection != null)
            {
                var entityArray = GetEntityArray();
                UnsafeUtility.SetFieldStruct(targetObject, m_EntityArrayInjection, ref entityArray);
            }
        }

    }
}