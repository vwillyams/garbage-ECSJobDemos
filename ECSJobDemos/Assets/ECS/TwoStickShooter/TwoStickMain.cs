using System.Collections.Concurrent;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using UnityEngine.ECS.Transform;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;

namespace TwoStickExample
{
    public sealed class TwoStickBootstrap
    {
        public static EntityArchetype PlayerArchetype;
        public static EntityArchetype ShotArchetype;
        public static EntityArchetype BasicEnemyArchetype;
        public static EntityArchetype ShotSpawnArchetype;

        public static InstanceRenderer PlayerLook;
        public static InstanceRenderer ShotLook;

        public static TwoStickExampleSettings Settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            PlayerArchetype = entityManager.CreateArchetype(typeof(WorldPos), typeof(PlayerInput), typeof(TransformMatrix));
            ShotArchetype = entityManager.CreateArchetype(typeof(WorldPos), typeof(Shot), typeof(TransformMatrix));
            ShotSpawnArchetype = entityManager.CreateArchetype(typeof(ShotSpawnData));
            BasicEnemyArchetype = entityManager.CreateArchetype(typeof(WorldPos), typeof(TransformMatrix));
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeWithScene()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            Settings = GameObject.Find("Settings").GetComponent<TwoStickExampleSettings>();

            PlayerLook = GetLookFromPrototype("PlayerRenderPrototype");
            ShotLook = GetLookFromPrototype("ShotRenderPrototype");

            Entity player = entityManager.CreateEntity(PlayerArchetype);
            WorldPos initialPos;
            initialPos.Position = new float2(0, 0);
            initialPos.Heading = new float2(0, 1);

            entityManager.SetComponent(player, initialPos);
            entityManager.AddSharedComponent(player, PlayerLook);
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
