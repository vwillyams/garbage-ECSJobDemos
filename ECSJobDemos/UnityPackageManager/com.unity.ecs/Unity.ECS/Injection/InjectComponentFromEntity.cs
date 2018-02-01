using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace UnityEngine.ECS
{
    internal class InjectFromEntityData
	{
	    internal class UpdateInjectionComponentDataFromEntity<T> : IUpdateInjection where T : struct, IComponentData
		{
			unsafe public void UpdateInjection(byte* targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
			{
				var array = entityManager.GetComponentDataFromEntity<T>(injection.isReadOnly);
				UnsafeUtility.CopyStructureToPtr(ref array, targetObject + injection.fieldOffset);
			}
		}

	    internal class UpdateInjectionFixedArrayFromEntity<T> : IUpdateInjection where T : struct
		{
			unsafe public void UpdateInjection(byte* targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
			{
				var array = entityManager.GetFixedArrayFromEntity<T>(injection.isReadOnly);
				UnsafeUtility.CopyStructureToPtr(ref array, targetObject + injection.fieldOffset);
			}
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

	    static public InjectionData CreateInjection(FieldInfo field, EntityManager entityManager)
		{
			var isReadOnly = field.GetCustomAttributes(typeof(ReadOnlyAttribute), true).Length != 0;

			if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(ComponentDataFromEntity<>))
			{
				var injection = new InjectionData(field, typeof(ComponentDataFromEntity<>), field.FieldType.GetGenericArguments()[0], isReadOnly);
				var injectionType = typeof(UpdateInjectionComponentDataFromEntity<>).MakeGenericType(injection.genericType);
				injection.injection = (IUpdateInjection) Activator.CreateInstance(injectionType);

				return injection;
			}
			else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(FixedArrayFromEntity<>))
			{
				var injection = new InjectionData(field, typeof(FixedArrayFromEntity<>), field.FieldType.GetGenericArguments()[0], isReadOnly);
				var injectionType = typeof(UpdateInjectionFixedArrayFromEntity<>).MakeGenericType(injection.genericType);
				injection.injection = (IUpdateInjection) Activator.CreateInstance(injectionType);

				return injection;
			}
			else
			{
			    ComponentSystemInjection.ThrowUnsupportedInjectException(field);
			    return default (InjectionData);
			}
		}

		public unsafe static void UpdateInjection(byte* pinnedSystemPtr, EntityManager entityManager, InjectionData[] injections)
		{
			foreach(var injection in injections)
				injection.injection.UpdateInjection(pinnedSystemPtr, entityManager, null, injection);
		}

		internal static void ExtractJobDependencyTypes(InjectionData[] injections, List<int> reading, List<int> writing)
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
