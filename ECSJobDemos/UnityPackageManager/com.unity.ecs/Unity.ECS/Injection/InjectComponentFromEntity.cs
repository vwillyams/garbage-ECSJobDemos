using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace UnityEngine.ECS
{
    struct InjectFromEntityData
	{
	    InjectionData[] m_InjectComponentDataFromEntity;
	    InjectionData[] m_InjectFixedArrayFromEntity;

	    public InjectFromEntityData(InjectionData[] componentDataFromEntity, InjectionData[] fixedArrayFromEntity)
	    {
	        m_InjectComponentDataFromEntity = componentDataFromEntity;
	        m_InjectFixedArrayFromEntity = fixedArrayFromEntity;
	    }

	    static public bool SupportsInjections(FieldInfo field)
	    {
	        if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(ComponentDataFromEntity<>))
	            return true;
	        else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(FixedArrayFromEntity<>))
	            return true;
	        else
	            return false;
	    }

	    static public void CreateInjection(FieldInfo field, EntityManager entityManager, List<InjectionData> componentDataFromEntity, List<InjectionData> fixedArrayFromEntity)
		{
			var isReadOnly = field.GetCustomAttributes(typeof(ReadOnlyAttribute), true).Length != 0;

			if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(ComponentDataFromEntity<>))
			{
				var injection = new InjectionData(field, typeof(ComponentDataFromEntity<>), field.FieldType.GetGenericArguments()[0], isReadOnly);
			    injection.indexInComponentGroup = TypeManager.GetTypeIndex(injection.genericType);
			    componentDataFromEntity.Add(injection);
			}
			else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(FixedArrayFromEntity<>))
			{
				var injection = new InjectionData(field, typeof(FixedArrayFromEntity<>), field.FieldType.GetGenericArguments()[0], isReadOnly);
			    injection.indexInComponentGroup = TypeManager.GetTypeIndex(injection.genericType);
			    fixedArrayFromEntity.Add(injection);
			}
			else
			{
			    ComponentSystemInjection.ThrowUnsupportedInjectException(field);
			}
		}

		public unsafe void UpdateInjection(byte* pinnedSystemPtr, EntityManager entityManager)
		{
		    for (var i = 0; i != m_InjectComponentDataFromEntity.Length; i++)
		    {
		        var array = entityManager.GetComponentDataFromEntity<ProxyComponentData>(m_InjectComponentDataFromEntity[i].indexInComponentGroup, m_InjectComponentDataFromEntity[i].isReadOnly);
		        UnsafeUtility.CopyStructureToPtr(ref array, pinnedSystemPtr + m_InjectComponentDataFromEntity[i].fieldOffset);
		    }

		    for (var i = 0; i != m_InjectFixedArrayFromEntity.Length; i++)
		    {
		        var array = entityManager.GetFixedArrayFromEntity<int>(m_InjectFixedArrayFromEntity[i].indexInComponentGroup, m_InjectFixedArrayFromEntity[i].isReadOnly);
		        UnsafeUtility.CopyStructureToPtr(ref array, pinnedSystemPtr + m_InjectFixedArrayFromEntity[i].fieldOffset);
		    }
		}

	    public void ExtractJobDependencyTypes(List<int> reading, List<int> writing)
	    {
	        ExtractJobDependencyTypes(m_InjectComponentDataFromEntity, reading, writing);
	        ExtractJobDependencyTypes(m_InjectFixedArrayFromEntity, reading, writing);
	    }

	    static void ExtractJobDependencyTypes(InjectionData[] injections, List<int> reading, List<int> writing)
		{
			foreach (var injection in injections)
			{
				ComponentType type = new ComponentType(injection.genericType);
				type.accessMode = injection.isReadOnly ? ComponentType.AccessMode.ReadOnly : ComponentType.AccessMode.ReadWrite;
				ComponentGroup.AddReaderWriter(type, reading, writing);
			}
		}

	}
}
