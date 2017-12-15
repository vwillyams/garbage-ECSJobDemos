using System;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEngine.ECS;

#if ASTEROIDS_SERVER
    class ServerWorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Debug.Log("ServerWorldBootstrap");

            var world = WorldCreator.FindAndCreateWorldFromNamespace("Asteriods.Server");
            World.UpdatePlayerLoop(world);
        }
    }
#else // Client + Server
    class LocalWorldBootstrap
    {
        public static World clientWorld;
        public static World serverWorld;

        static void DomainUnloadShutdown()
        {
            if (clientWorld != null && serverWorld != null)
            {
                serverWorld.Dispose();
                clientWorld.Dispose();
                World.UpdatePlayerLoop();
            }
            else
            {
                Debug.LogError("World has already been destroyed");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown);

            serverWorld = WorldCreator.FindAndCreateWorldFromNamespace("Asteriods.Server");
            clientWorld = WorldCreator.FindAndCreateWorldFromNamespace("Asteriods.Client");
            World.Active = null;

            World.UpdatePlayerLoop(serverWorld, clientWorld);
        }
    }
#endif

    public class WorldCreator
    {
        public static World FindAndCreateWorldFromNamespace(string name)
        {
            var world = new World();
            World.Active = world;

            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                var allTypes = ass.GetTypes();

                // Make it based on an attribute.
                // Create all ComponentSystem
                var systemTypes = allTypes.Where(
                    t => t.IsSubclassOf(typeof(ComponentSystemBase)) &&
                    !t.IsAbstract &&
                    !t.ContainsGenericParameters &&
                    (t.Namespace != null && t.Namespace == name) &&
                    t.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0);
                foreach (var type in systemTypes)
                {
                    GetBehaviourManagerAndLogException(world, type);
                }
            }
            return world;
        }

        public static void GetBehaviourManagerAndLogException(World world, Type type)
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
    }

public class GameMain : MonoBehaviour
{
    protected void OnEnable()
    {
        Debug.Log("GameMain");
        //World w = new World();
    }
}
