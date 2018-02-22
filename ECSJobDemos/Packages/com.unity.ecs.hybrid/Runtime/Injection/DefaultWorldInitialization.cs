using System;
using System.Linq;
using System.Collections.Generic;

namespace Unity.Entities.Hybrid
{
    static class DefaultWorldInitialization
    {
        const string k_DefaultWorldName = "Default World";
        static World s_CreatedWorld;

        static void DomainUnloadShutdown()
        {
            if (World.Active == s_CreatedWorld)
            {
                World.Active.Dispose ();
                World.Active = null;

                World.UpdatePlayerLoop();
            }
            else
            {
                Debug.LogError("World has already been destroyed");
            }
        }

        static void GetBehaviourManagerAndLogException(World world, Type type)
        {
            try
            {
                world.GetOrCreateManager(type);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void Initialize()
        {
            var world = new World(k_DefaultWorldName);
            World.Active = world;
            s_CreatedWorld = world;

            // Register hybrid injection hooks
            InjectionHookSupport.RegisterHook(new GameObjectArrayInjectionHook());

            PlayerLoopManager.RegisterDomainUnload (DomainUnloadShutdown, 10000);

            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                var allTypes = ass.GetTypes();

                // Create all ComponentSystem
                var systemTypes = allTypes.Where(t => t.IsSubclassOf(typeof(ComponentSystemBase)) && !t.IsAbstract && !t.ContainsGenericParameters && t.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0);
                foreach (var type in systemTypes)
                {
                    GetBehaviourManagerAndLogException(world, type);
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
                        GetBehaviourManagerAndLogException(world, type);
                    }
                    else if (genericTypes.Count == 3)
                    {
                        var type = typeof(GenericProcessComponentSystem<,,>).MakeGenericType(genericTypes.ToArray());
                        GetBehaviourManagerAndLogException(world, type);
                    }
                    else
                    {
                        Debug.LogError(
                            $"{jobType} implements the IAutoComponentSystemJob interface, for it to run, you also need to IJobProcessComponentData");
                    }
                }
            }

            World.UpdatePlayerLoop(world);
        }
    }
}
