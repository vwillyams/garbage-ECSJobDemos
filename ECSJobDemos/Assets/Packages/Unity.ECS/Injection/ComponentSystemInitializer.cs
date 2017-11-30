using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine.ECS
{
    class ComponentSystemInitializer
    {
        static void GetBehaviourManagerAndLogException(Type type)
        {
            try
            {
                World.GetBehaviourManager(type);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }        
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                var allTypes = ass.GetTypes();

                // Create all ComponentSystem
                var systemTypes = allTypes.Where(t => t.IsSubclassOf(typeof(ComponentSystemBase)) && !t.IsAbstract && !t.ContainsGenericParameters && t.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0);
                foreach (var type in systemTypes)
                {
                    GetBehaviourManagerAndLogException(type);
                }

                // Create All IAutoComponentSystemJob
                var genericTypes = new List<Type>();
                var jobTypes = allTypes.Where(t => typeof(IAutoComponentSystemJob).IsAssignableFrom(t) && !t.IsAbstract && t.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0);
                foreach (var jobType in jobTypes)
                {
                    genericTypes.Clear();
                    genericTypes.Add(jobType);
                    foreach (var iType in jobType.GetInterfaces())
                    {
                        if (iType.Name.StartsWith("IJobProcessComponentData"))
                        {
                            genericTypes.AddRange(iType.GetGenericArguments());
                        }
                    }

                    if (genericTypes.Count == 2)
                    {
                        var type = typeof(GenericProcessComponentSystem<,>).MakeGenericType(genericTypes.ToArray());
                        GetBehaviourManagerAndLogException(type);
                    }
                    else if (genericTypes.Count == 3)
                    {
                        var type = typeof(GenericProcessComponentSystem<,,>).MakeGenericType(genericTypes.ToArray());
                        GetBehaviourManagerAndLogException(type);
                    }
                    else
                    {
                        Debug.LogError(string.Format("{0} implements the IAutoComponentSystemJob interface, for it to run, you also need to IJobProcessComponentData", jobType));   
                    }
                }
            }
        }
    }
}
