using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using UnityEngine.ECS.Transform;

namespace TwoStickPureExample
{
    public sealed class TwoStickBootstrap
    {
        public static EntityArchetype PlayerArchetype;
        public static EntityArchetype ShotArchetype;
        public static EntityArchetype BasicEnemyArchetype;
        public static EntityArchetype ShotSpawnArchetype;

        public static InstanceRenderer PlayerLook;
        public static InstanceRenderer PlayerShotLook;
        public static InstanceRenderer EnemyShotLook;
        public static InstanceRenderer EnemyLook;

        public static TwoStickExampleSettings Settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            PlayerArchetype = entityManager.CreateArchetype(typeof(Transform2D), typeof(PlayerInput), typeof(Faction), typeof(Health), typeof(TransformMatrix));
            ShotArchetype = entityManager.CreateArchetype(typeof(Transform2D), typeof(Shot), typeof(TransformMatrix), typeof(Faction));
            ShotSpawnArchetype = entityManager.CreateArchetype(typeof(ShotSpawnData));
            BasicEnemyArchetype = entityManager.CreateArchetype(typeof(Enemy), typeof(Health), typeof(EnemyShootState), typeof(Faction), typeof(Transform2D), typeof(TransformMatrix));
        }

        public static void NewGame()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();
            Entity player = entityManager.CreateEntity(PlayerArchetype);
            Transform2D initialXform;
            initialXform.Position = new float2(0, 0);
            initialXform.Heading = new float2(0, 1);

            entityManager.SetComponent(player, initialXform);
            entityManager.SetComponent(player, new Faction { Value = Faction.kPlayer });
            entityManager.SetComponent(player, new Health { Value = Settings.playerInitialHealth });
            entityManager.AddSharedComponent(player, PlayerLook);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeWithScene()
        {
            var settingsGO = GameObject.Find("Settings");
            Settings = settingsGO?.GetComponent<TwoStickExampleSettings>();
            if (!Settings)
                return;

            PlayerLook = GetLookFromPrototype("PlayerRenderPrototype");
            PlayerShotLook = GetLookFromPrototype("PlayerShotRenderPrototype");
            EnemyShotLook = GetLookFromPrototype("EnemyShotRenderPrototype");
            EnemyLook = GetLookFromPrototype("EnemyRenderPrototype");

            var entityManager = World.Active.GetOrCreateManager<EntityManager>();
            var arch = entityManager.CreateArchetype(typeof(EnemySpawnSystemState));
            var stateEntity = entityManager.CreateEntity(arch);
            var oldState = Random.state;
            Random.InitState(0xaf77);
            entityManager.SetComponent(stateEntity, new EnemySpawnSystemState
            {
                Cooldown = 0.0f,
                SpawnedEnemyCount = 0,
                RandomState = Random.state
            });
            Random.state = oldState;

            World.Active.GetOrCreateManager<UpdatePlayerHUD>().SetupGameObjects();
        }

        private static InstanceRenderer GetLookFromPrototype(string protoName)
        {
            var proto = GameObject.Find(protoName);
            var result = proto.GetComponent<InstanceRendererComponent>().Value;
            Object.Destroy(proto);
            return result;
        }
    }
}
