using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.ECS;

public class WorldBootstrap
{
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
#           if ASTEROIDS_SERVER
                ServerWorldBootstrap.Initialize();
#           elif ASTEROIDS_CLIENT
                ClientWorldBootstrap.Initialize();
#           else
                LocalWorldBootstrap.Initialize();
#           endif
        }
}

#if ASTEROIDS_SERVER
    public class ServerWorldBootstrap
    {
        public static World serverWorld;

        public static void DomainUnloadShutdown()
        {
            if (serverWorld != null)
            {
                serverWorld.Dispose();
                ServerSettings.Instance().networkServer.Dispose();
                World.UpdatePlayerLoop();
            }
            else
            {
                Debug.LogError("World has already been destroyed");
            }
        }

        public static void Initialize()
        {
            Debug.LogWarning("Server Running");
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown);

            serverWorld = new World("Server");
            ServerSettings.Create(serverWorld);
            WorldCreator.FindAndCreateWorldFromNamespace(serverWorld, "Asteriods.Server");
            World.Active = null;
            World.UpdatePlayerLoop(serverWorld);
        }
    }

#elif ASTEROIDS_CLIENT

    class ClientWorldBootstrap
    {
        public static World clientWorld;

        public static void DomainUnloadShutdown()
        {
            if (clientWorld != null)
            {
                clientWorld.Dispose();
                ClientSettings.Instance().networkClient.Dispose();
                World.UpdatePlayerLoop();
            }
            else
            {
                Debug.LogError("World has already been destroyed");
            }
        }

        public static void Initialize()
        {
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown);

            clientWorld = new World("Client");
            ClientSettings.Create(clientWorld);
            WorldCreator.FindAndCreateWorldFromNamespace(clientWorld, "Asteriods.Client");
            World.Active = null;
            World.UpdatePlayerLoop(clientWorld);
        }
    }

#else // Client + Server

    class LocalWorldBootstrap
    {
        public static World clientWorld;
        public static World serverWorld;

        public static void DomainUnloadShutdown()
        {
            if (clientWorld != null && serverWorld != null)
            {
                serverWorld.Dispose();
                clientWorld.Dispose();
                ClientSettings.Instance().networkClient.Dispose();
                ServerSettings.Instance().networkServer.Dispose();
                World.UpdatePlayerLoop();
            }
            else
            {
                Debug.LogError("World has already been destroyed");
            }
        }

        public static void Initialize()
        {
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown);

            serverWorld = new World("Server");
            clientWorld = new World("Client");

            ServerSettings.Create(serverWorld);
            ClientSettings.Create(clientWorld);

            WorldCreator.FindAndCreateWorldFromNamespace(serverWorld, "Asteriods.Server");
            WorldCreator.FindAndCreateWorldFromNamespace(clientWorld, "Asteriods.Client");

            World.Active = null;

            World.UpdatePlayerLoop(serverWorld, clientWorld);
        }
    }

#endif

public class WorldCreator
{
    public static void FindAndCreateWorldFromNamespace(World world, string name)
    {
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
    }
}
