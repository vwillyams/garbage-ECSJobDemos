﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ECS;
using UnityEditor.Experimental.Build.Player;

namespace UnityEditor.ECS
{
    [InitializeOnLoad]
    public sealed class ExtraTypesProvider
    {
        static ExtraTypesProvider()
        {
            PlayerBuildInterface.ExtraTypesProvider += () =>
            {
                var extraTypes = new HashSet<string>();

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.GetReferencedAssemblies().Any(a => a.Name.Contains(nameof(UnityEngine.ECS))) &&
                        assembly.GetName().Name != nameof(UnityEngine.ECS))
                        continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IAutoComponentSystemJob).IsAssignableFrom(type) && !type.IsAbstract &&
                            type.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0)
                        {
                            var genericArguments = new List<Type>
                            {
                                type
                            };
                            foreach (var @interface in type.GetInterfaces())
                            {
                                if (@interface.Name.StartsWith("IJobProcessComponentData"))
                                    genericArguments.AddRange(@interface.GetGenericArguments());
                            }

                            if (genericArguments.Count == 2)
                            {
                                var generatedType =
                                    typeof(GenericProcessComponentSystem<,>)
                                        .MakeGenericType(genericArguments.ToArray());
                                extraTypes.Add(generatedType.ToString());
                                extraTypes.Add(typeof(ComponentDataArray<>).MakeGenericType(genericArguments[0]).ToString());
                            }
                            else if (genericArguments.Count == 3)
                            {
                                var generatedType =
                                    typeof(GenericProcessComponentSystem<,,>).MakeGenericType(
                                        genericArguments.ToArray());
                                extraTypes.Add(generatedType.ToString());
                                extraTypes.Add(typeof(ComponentDataArray<>).MakeGenericType(genericArguments[0]).ToString());
                                extraTypes.Add(typeof(ComponentDataArray<>).MakeGenericType(genericArguments[1]).ToString());
                            }
                        }
                    }
                }

                return extraTypes;
            };
        }
    }
}
