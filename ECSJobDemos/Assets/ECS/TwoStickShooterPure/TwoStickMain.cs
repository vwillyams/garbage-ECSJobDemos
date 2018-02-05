using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform;
using UnityEngine.ECS.Transform2D;

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

            PlayerArchetype = entityManager.CreateArchetype(typeof(Position2D), typeof(Heading2D), typeof(PlayerInput), typeof(Faction), typeof(Health), typeof(TransformMatrix));
            ShotArchetype = entityManager.CreateArchetype(typeof(Position2D), typeof(Heading2D), typeof(Shot), typeof(TransformMatrix), typeof(Faction), typeof(MoveSpeed), typeof(MoveForward));
            ShotSpawnArchetype = entityManager.CreateArchetype(typeof(ShotSpawnData));
            BasicEnemyArchetype = entityManager.CreateArchetype(typeof(Enemy), typeof(Health), typeof(EnemyShootState), typeof(Faction), typeof(Position2D), typeof(Heading2D), typeof(TransformMatrix), typeof(MoveSpeed), typeof(MoveForward) );
        }

        public static void NewGame()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();
            Entity player = entityManager.CreateEntity(PlayerArchetype);
            entityManager.SetComponentData(player, new Position2D {position = new float2(0.0f, 0.0f)});
            entityManager.SetComponentData(player, new Heading2D  {heading = new float2(0.0f, 1.0f)});
            entityManager.SetComponentData(player, new Faction { Value = Faction.kPlayer });
            entityManager.SetComponentData(player, new Health { Value = Settings.playerInitialHealth });
            entityManager.AddSharedComponentData(player, PlayerLook);
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
            entityManager.SetComponentData(stateEntity, new EnemySpawnSystemState
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
