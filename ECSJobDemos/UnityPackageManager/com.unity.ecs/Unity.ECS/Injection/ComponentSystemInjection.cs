﻿using System;
using System.Reflection;
using System.Collections.Generic;

namespace Unity.ECS
{
    static class ComponentSystemInjection
    {
        public static string GetFieldString(FieldInfo info)
        {
            return $"{info.DeclaringType.Name}.{info.Name}";
        }

        public static void Inject(ComponentSystemBase componentSystem, World world, EntityManager entityManager, out InjectComponentGroupData[] outInjectGroups, out InjectFromEntityData outInjectFromEntityData)
        {
            var componentSystemType = componentSystem.GetType();

            ValidateNoStaticInjectDependencies(componentSystemType);

            var fields = componentSystemType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var injectGroups = new List<InjectComponentGroupData>();

            var injectFromEntity = new List<InjectionData>();
            var injectFromFixedArray = new List<InjectionData>();

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttributes(typeof(InjectAttribute), true);
                if (attr.Length == 0)
                    continue;

                if (field.FieldType.IsClass)
                {
                    InjectConstructorDependencies(componentSystem, world, field);
                }
                else
                {
                    if (InjectFromEntityData.SupportsInjections(field))
                        InjectFromEntityData.CreateInjection(field, entityManager, injectFromEntity, injectFromFixedArray);
                    else
                        injectGroups.Add(InjectComponentGroupData.CreateInjection(field.FieldType, field, entityManager));
                }
            }

            outInjectGroups = injectGroups.ToArray();

            outInjectFromEntityData = new InjectFromEntityData(injectFromEntity.ToArray(), injectFromFixedArray.ToArray());
        }

        static void ValidateNoStaticInjectDependencies(Type type)
        {
#if UNITY_EDITOR
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                if (field.GetCustomAttributes(typeof(InjectAttribute), true).Length != 0)
                    throw new ArgumentException(
                        $"[Inject] may not be used on static variables: {GetFieldString(field)}");
            }
#endif
        }

        static void InjectConstructorDependencies(ScriptBehaviourManager manager, World world, FieldInfo field)
        {
            if (field.FieldType.IsSubclassOf(typeof(ScriptBehaviourManager)))
            {
                field.SetValue(manager, world.GetOrCreateManager(field.FieldType));
            }
            else
            {
                ThrowUnsupportedInjectException(field);
            }
        }

        public static void ThrowUnsupportedInjectException(FieldInfo field)
        {
            throw new ArgumentException(
                $"[Inject] is not supported for type '{field.FieldType}'. At: {GetFieldString(field)}");
        }
    }
}
