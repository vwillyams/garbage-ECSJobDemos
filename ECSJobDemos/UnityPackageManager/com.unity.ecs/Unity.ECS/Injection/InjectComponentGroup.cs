using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Jobs;

namespace UnityEngine.ECS
{
	internal class InjectComponentGroupData
    {
	    int 				m_EntityArrayOffset;
	    int 				m_TransformAccessArrayOffset;
	    int 				m_LengthOffset;
        int                 m_GameObjectArrayOffset;

	    InjectionData[]     m_ComponentInjections;
	    ComponentType[]     m_ComponentRequirements;

	    ComponentGroup 		m_EntityGroup;

	    int 				m_GroupFieldOffset;


        internal class UpdateInjectionComponentDataArray<T> : IUpdateInjection where T : struct, IComponentData
        {
	        unsafe public void UpdateInjection(byte* groupData, EntityManager entityManager, ComponentGroup group, InjectionData injection)
            {
                var array = group.GetComponentDataArray<T>();
	            UnsafeUtility.CopyStructureToPtr(ref array, groupData + injection.fieldOffset);
            }
        }

        internal class UpdateInjectionComponentDataFixedArray<T> : IUpdateInjection where T : struct
	    {
		    unsafe public void UpdateInjection(byte* groupData, EntityManager entityManager, ComponentGroup group, InjectionData injection)
		    {
			    var array = group.GetFixedArrayArray<T>();
			    UnsafeUtility.CopyStructureToPtr(ref array, groupData + injection.fieldOffset);
		    }
	    }

        internal class UpdateInjectionComponentArray<T> : IUpdateInjection where T : UnityEngine.Component
        {
            unsafe public void UpdateInjection(byte* groupData, EntityManager entityManager, ComponentGroup group, InjectionData injection)
            {
                var array = group.GetComponentArray<T>();
	            UnsafeUtility.CopyStructureToPtr(ref array, groupData + injection.fieldOffset);
            }
        }

		InjectComponentGroupData(EntityManager entityManager, FieldInfo groupField, InjectionData[] componentInjections, FieldInfo entityArrayInjection, FieldInfo transformAccessArrayInjection, FieldInfo gameObjectArrayInjection, FieldInfo lengthInjection, ComponentType[] componentRequirements)
		{
			var requiredComponentTypes = new ComponentType[componentInjections.Length + componentRequirements.Length];

            for (int i = 0; i != componentInjections.Length; i++)
                requiredComponentTypes[i] = new ComponentType(componentInjections[i].genericType, componentInjections[i].isReadOnly ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite);

            for (int i = 0; i != componentRequirements.Length; i++)
                requiredComponentTypes[componentInjections.Length + i] = componentRequirements[i];

            m_EntityGroup = entityManager.CreateComponentGroup(requiredComponentTypes);

            m_ComponentInjections = componentInjections;

			if (entityArrayInjection != null)
				m_EntityArrayOffset = UnsafeUtility.GetFieldOffset(entityArrayInjection);
			else
				m_EntityArrayOffset = -1;

			if (lengthInjection != null)
				m_LengthOffset = UnsafeUtility.GetFieldOffset(lengthInjection);
			else
				m_LengthOffset = -1;

		    if (gameObjectArrayInjection != null)
		        m_GameObjectArrayOffset = UnsafeUtility.GetFieldOffset(gameObjectArrayInjection);
		    else
		        m_GameObjectArrayOffset = -1;

			if (transformAccessArrayInjection != null)
				m_TransformAccessArrayOffset = UnsafeUtility.GetFieldOffset(transformAccessArrayInjection);
			else
				m_TransformAccessArrayOffset = -1;

			m_GroupFieldOffset = UnsafeUtility.GetFieldOffset(groupField);
		}

		public void Dispose()
		{
			m_EntityGroup.Dispose ();
			m_EntityGroup = null;
		}

		public ComponentGroup EntityGroup          { get { return m_EntityGroup; } }

        public unsafe void UpdateInjection(byte* systemPtr)
        {
	        var groupStructPtr = systemPtr + m_GroupFieldOffset;

            for (var i = 0; i != m_ComponentInjections.Length; i++)
                m_ComponentInjections[i].injection.UpdateInjection(groupStructPtr, null, m_EntityGroup, m_ComponentInjections[i]);

	        if (m_TransformAccessArrayOffset != -1)
	        {
		        var transformsArray = m_EntityGroup.GetTransformAccessArray();
		        UnsafeUtility.CopyStructureToPtr(ref transformsArray, groupStructPtr + m_TransformAccessArrayOffset);
	        }

            if (m_EntityArrayOffset != -1)
            {
                var entityArray = m_EntityGroup.GetEntityArray();
                UnsafeUtility.CopyStructureToPtr(ref entityArray, groupStructPtr + m_EntityArrayOffset);
            }

            if (m_GameObjectArrayOffset != -1)
            {
                var gameObjectArray = m_EntityGroup.GetGameObjectArray();
                UnsafeUtility.CopyStructureToPtr(ref gameObjectArray, groupStructPtr + m_GameObjectArrayOffset);
            }

	        if (m_LengthOffset != -1)
	        {
		        int length = m_EntityGroup.CalculateLength();
		        UnsafeUtility.CopyStructureToPtr(ref length, groupStructPtr + m_LengthOffset);
	        }
        }

	    static public InjectComponentGroupData CreateInjection(Type injectedGroupType, FieldInfo groupField, EntityManager entityManager)
	    {
		    FieldInfo entityArrayField;
	        FieldInfo gameObjectArrayField;
		    FieldInfo transformAccessArrayField;
		    FieldInfo lengthField;
		    var componentInjections = new List<InjectionData>();
		    var componentRequirements = new List<ComponentType>();
		    var error = CollectInjectedGroup(injectedGroupType, out entityArrayField, out transformAccessArrayField, out gameObjectArrayField, out lengthField, componentInjections, componentRequirements);
		    if (error != null)
			    throw new System.ArgumentException(error);

		    return new InjectComponentGroupData(entityManager, groupField, componentInjections.ToArray(), entityArrayField, transformAccessArrayField, gameObjectArrayField, lengthField, componentRequirements.ToArray());
	    }

	    static string CollectInjectedGroup(Type injectedGroupType, out FieldInfo entityArrayField, out FieldInfo transformAccessArrayField, out FieldInfo gameObjectArrayField, out FieldInfo lengthField, List<InjectionData> componentInjections, List<ComponentType> componentRequirements)
	    {
	        //@TODO: Improve error messages...
		    var fields = injectedGroupType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		    transformAccessArrayField = null;
		    entityArrayField = null;
	        gameObjectArrayField = null;
		    lengthField = null;
	        var explicitTransformRequirement = false;
	        var implicitTransformRequirement = false;

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
			    else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition () == typeof(SubtractiveComponent<>))
			    {
					componentRequirements.Add (ComponentType.Subtractive(field.FieldType.GetGenericArguments()[0]));
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

				    if (injection.genericType == typeof(Transform))
				        explicitTransformRequirement = true;
				}
				else if (field.FieldType == typeof(TransformAccessArray))
				{
					if (isReadOnly)
						return "[ReadOnly] may not be used on a TransformAccessArray only on ComponentDataArray<>";
					// Error on multiple transformAccessArray
					if (transformAccessArrayField != null)
						return "A [Inject] struct, may only contain a single TransformAccessArray";

					transformAccessArrayField = field;
				    implicitTransformRequirement = true;
				}
				else if (field.FieldType == typeof(EntityArray))
				{
					// Error on multiple EntityArray
					if (entityArrayField != null)
						return "A [Inject] struct, may only contain a single EntityArray";

					entityArrayField = field;
				}
			    else if (field.FieldType == typeof(GameObjectArray))
			    {
			        if (isReadOnly)
			            return "[ReadOnly] may not be used on GameObjectArray, it can only be used on ComponentDataArray<>";
			        // Error on multiple GameObjectArray
			        if (gameObjectArrayField != null)
			            return "A [Inject] struct, may only contain a single GameObjectArray";

			        gameObjectArrayField = field;
			        implicitTransformRequirement = true;
			    }
				else if (field.FieldType == typeof(int))
			    {
				    // Error on multiple EntityArray
				    if (field.Name != "Length")
					    return "A [Inject] struct, supports only a specialized int storing the length of the group. (\"int Length;\")";
				    lengthField = field;
			    }
			    else
			    {
				    return
					    "[Inject] may only be used on ComponentDataArray<>, ComponentArray<>, TransformAccessArray, EntityArray, GameObjectArray and int Length.";
			    }
		    }

	        if (!explicitTransformRequirement && implicitTransformRequirement)
	        {
	            componentRequirements.Add(typeof(Transform));
	        }

		    return null;
	    }

    }
}
