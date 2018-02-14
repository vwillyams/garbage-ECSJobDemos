using System.Collections.Generic;
using Unity.Collections;
using Unity.ECS;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{
    public class ShotMoveSystem : ComponentSystem
    {
        struct Data
        {
            [ReadOnly] public Shot Shot;
            public Transform2D Transform;
        }

        protected override void OnUpdate()
        {
            foreach (var entity in GetEntities<Data>())
            {
                entity.Transform.Position += entity.Transform.Heading * entity.Shot.Speed;
            }
        }
    }

    public class ShotSpawnData
    {
        public float2 Position;
        public float2 Heading;
        public Faction Faction;
    }

    public static class ShotSpawnSystem
    {
        public static void SpawnShot(ShotSpawnData data)
        {
            var settings = TwoStickBootstrap.Settings;
            var prefab = data.Faction.Value == Faction.Type.Player
                ? settings.PlayerShotPrefab
                : settings.EnemyShotPrefab;
            var newShot = Object.Instantiate(prefab);

            var shotXform = newShot.GetComponent<Transform2D>();
            shotXform.Position = data.Position;
            shotXform.Heading = data.Heading;

            var shotFaction = newShot.GetComponent<Faction>();
            shotFaction.Value = data.Faction.Value;
        }
    }

    [UpdateAfter(typeof(ShotMoveSystem))]
    public class ShotDestroySystem : ComponentSystem
    {
        struct Data
        {
            public Shot Shot;
        }

        protected override void OnUpdate()
        {
            float dt = Time.deltaTime;

            var toDestroy = new List<GameObject>();
            foreach (var entity in GetEntities<Data>())
            {
                var s = entity.Shot;
                s.TimeToLive -= dt;
                if (s.TimeToLive <= 0.0f)
                {
                    toDestroy.Add(s.gameObject);
                }
            }

            foreach (var go in toDestroy)
            {
                Object.Destroy(go);
            }
        }
    }

}
