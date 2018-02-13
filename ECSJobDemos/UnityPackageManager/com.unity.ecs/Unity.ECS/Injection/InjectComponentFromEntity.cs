using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.ECS;

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
				var injection = new InjectionData(field, field.FieldType.GetGenericArguments()[0], isReadOnly);
			    componentDataFromEntity.Add(injection);
			}
			else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(FixedArrayFromEntity<>))
			{
				var injection = new InjectionData(field, field.FieldType.GetGenericArguments()[0], isReadOnly);
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
		        var array = entityManager.GetComponentDataFromEntity<ProxyComponentData>(m_InjectComponentDataFromEntity[i].ComponentType.TypeIndex, m_InjectComponentDataFromEntity[i].IsReadOnly);
		        UnsafeUtility.CopyStructureToPtr(ref array, pinnedSystemPtr + m_InjectComponentDataFromEntity[i].FieldOffset);
		    }

		    for (var i = 0; i != m_InjectFixedArrayFromEntity.Length; i++)
		    {
		        var array = entityManager.GetFixedArrayFromEntity<int>(m_InjectFixedArrayFromEntity[i].ComponentType.TypeIndex, m_InjectFixedArrayFromEntity[i].IsReadOnly);
		        UnsafeUtility.CopyStructureToPtr(ref array, pinnedSystemPtr + m_InjectFixedArrayFromEntity[i].FieldOffset);
		    }
		}

	    public void ExtractJobDependencyTypes(List<int> reading, List<int> writing)
	    {
	        foreach (var injection in m_InjectComponentDataFromEntity)
	            ComponentGroup.AddReaderWriter(injection.ComponentType, reading, writing);
	        foreach (var injection in m_InjectFixedArrayFromEntity)
	            ComponentGroup.AddReaderWriter(injection.ComponentType, reading, writing);
	    }
	}
}
