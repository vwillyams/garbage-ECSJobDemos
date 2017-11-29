using System;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace UnityEngine.ECS
{
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class InjectComponentFromEntityAttribute : System.Attribute
	{

	}
	
	class InjectFromEntityData
	{
		class UpdateInjectionComponentDataFromEntity<T> : IUpdateInjection where T : struct, IComponentData
		{
			public void UpdateInjection(object targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
			{
				var array = entityManager.GetComponentDataArrayFromEntity<T>(injection.isReadOnly);
				UnsafeUtility.SetFieldStruct(targetObject, injection.field, ref array);
			}
		}

		class UpdateInjectionFixedArrayFromEntity<T> : IUpdateInjection where T : struct
		{
			public void UpdateInjection(object targetObject, EntityManager entityManager, ComponentGroup group, InjectionData injection)
			{
				var array = entityManager.GetFixedArrayFromEntity<T>(injection.isReadOnly);
				UnsafeUtility.SetFieldStruct(targetObject, injection.field, ref array);
			}
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
				throw new System.InvalidOperationException($"{field.DeclaringType}.{field.Name} doesn't support [InjectComponentFromEntity]");
			}
		}

		public static void UpdateInjection(object system, EntityManager entityManager, InjectionData[] injections)
		{
			foreach(var injection in injections)
				injection.injection.UpdateInjection(system, entityManager, null, injection);
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