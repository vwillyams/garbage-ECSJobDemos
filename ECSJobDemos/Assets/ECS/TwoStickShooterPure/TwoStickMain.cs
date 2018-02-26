﻿using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Rendering;
using UnityEngine.ECS.SimpleMovement;
using Unity.Transforms;
using Unity.Transforms2D;

namespace TwoStickPureExample
{
    public sealed class TwoStickBootstrap
    {
        public static EntityArchetype PlayerArchetype;
        public static EntityArchetype BasicEnemyArchetype;
        public static EntityArchetype ShotSpawnArchetype;

        public static MeshInstanceRenderer PlayerLook;
        public static MeshInstanceRenderer PlayerShotLook;
        public static MeshInstanceRenderer EnemyShotLook;
        public static MeshInstanceRenderer EnemyLook;

        public static TwoStickExampleSettings Settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // This method creates archetypes for entities we will spawn frequently in this game.
            // Archetypes are optional but can speed up entity spawning substantially.

            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            // Create player archetype
            PlayerArchetype = entityManager.CreateArchetype(
                typeof(Position2D), typeof(Heading2D), typeof(PlayerInput),
                typeof(Faction), typeof(Health), typeof(TransformMatrix));

            // Create an archetype for "shot spawn request" entities
            ShotSpawnArchetype = entityManager.CreateArchetype(typeof(ShotSpawnData));

            // Create an archetype for basic enemies.
            BasicEnemyArchetype = entityManager.CreateArchetype(
                typeof(Enemy), typeof(Health), typeof(EnemyShootState),
                typeof(Faction), typeof(Position2D), typeof(Heading2D),
                typeof(TransformMatrix), typeof(MoveSpeed), typeof(MoveForward));
        }

        // Begin a new game.
        public static void NewGame()
        {
            // Access the ECS entity manager
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            // Create an entity based on the player archetype. It will get default-constructed
            // defaults for all the component types we listed.
            Entity player = entityManager.CreateEntity(PlayerArchetype);

            // We can tweak a few components to make more sense like this.
            entityManager.SetComponentData(player, new Position2D {Value = new float2(0.0f, 0.0f)});
            entityManager.SetComponentData(player, new Heading2D  {Value = new float2(0.0f, 1.0f)});
            entityManager.SetComponentData(player, new Faction { Value = Faction.kPlayer });
            entityManager.SetComponentData(player, new Health { Value = Settings.playerInitialHealth });

            // Finally we add a shared component which dictates the rendered look
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
            var arch = entityManager.CreateArchetype(typeof(EnemySpawnCooldown), typeof(EnemySpawnSystemState));
            var stateEntity = entityManager.CreateEntity(arch);
            var oldState = Random.state;
            Random.InitState(0xaf77);
            entityManager.SetComponentData(stateEntity, new EnemySpawnCooldown { Value = 0.0f });
            entityManager.SetComponentData(stateEntity, new EnemySpawnSystemState
            {
                SpawnedEnemyCount = 0,
                RandomState = Random.state
            });
            Random.state = oldState;

            World.Active.GetOrCreateManager<UpdatePlayerHUD>().SetupGameObjects();
        }

        private static MeshInstanceRenderer GetLookFromPrototype(string protoName)
        {
            var proto = GameObject.Find(protoName);
            var result = proto.GetComponent<MeshInstanceRendererComponent>().Value;
            Object.Destroy(proto);
            return result;
        }
    }
}
