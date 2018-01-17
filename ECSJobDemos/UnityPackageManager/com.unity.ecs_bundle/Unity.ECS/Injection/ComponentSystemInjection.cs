using System.Reflection;
using System;
using System.Collections.Generic;

namespace UnityEngine.ECS
{
    static class ComponentSystemInjection
    {
        static public void Inject(Type componentSystemType, EntityManager entityManager, out InjectComponentGroupData[] outInjectGroups, out InjectionData[] outInjectFromEntity)
        {
            var fields = componentSystemType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var injectGroups = new List<InjectComponentGroupData>();
            var injectFromEntity = new List<InjectionData>();
            foreach (var field in fields)
            {
                object[] attr;
			    
                // Component group injection
                attr = field.GetCustomAttributes(typeof(InjectComponentGroupAttribute), true);
                if (attr.Length != 0)
                    injectGroups.Add(InjectComponentGroupData.CreateInjection(field.FieldType, field, entityManager));
			    
                // Component from entity injection
                attr = field.GetCustomAttributes(typeof(InjectComponentFromEntityAttribute), true);
                if (attr.Length != 0)
                    injectFromEntity.Add(InjectFromEntityData.CreateInjection(field, entityManager));
            }

            outInjectGroups = injectGroups.ToArray();
            outInjectFromEntity = injectFromEntity.ToArray();
        }
    }
}