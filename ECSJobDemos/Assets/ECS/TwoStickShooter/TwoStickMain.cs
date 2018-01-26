using System.Collections.Concurrent;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using UnityEngine.ECS.Transform;
using UnityEngine.Experimental.LowLevel;
using UnityEngine.SceneManagement;

namespace TwoStickExample
{


    public sealed class TwoStickBootstrap
    {
        public static EntityArchetype PlayerArchetype;
        public static EntityArchetype BasicEnemyArchetype;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            PlayerArchetype = entityManager.CreateArchetype(typeof(WorldPos), typeof(PlayerInput), typeof(TransformMatrix));
            BasicEnemyArchetype = entityManager.CreateArchetype(typeof(WorldPos), typeof(TransformMatrix));

        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeWithScene()
        {

            var entityManager = World.Active.GetOrCreateManager<EntityManager>();

            Entity player = entityManager.CreateEntity(PlayerArchetype);
            WorldPos initialPos;
            initialPos.Position = new float2(0, 0);
            initialPos.Heading = new float2(0, 1);

            entityManager.SetComponent(player, initialPos);

            var playerRenderPrototype = GameObject.Find("PlayerRenderPrototype");
            var wrapper = playerRenderPrototype.GetComponent<InstanceRendererComponent>();
            entityManager.AddSharedComponent(player, wrapper.Value);
            Object.Destroy(playerRenderPrototype);
        }
    }
}

