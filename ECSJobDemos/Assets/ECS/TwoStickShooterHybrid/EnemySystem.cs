using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Hybrid;
using Unity.Mathematics;
using UnityEngine;

namespace TwoStickHybridExample
{
    // Spawns new enemies.
    [DisableSystemWhenEmpty]
    public class EnemySpawnSystem : ComponentSystem
    {

        public struct State
        {
            public int Length;
            public ComponentArray<EnemySpawnSystemState> S;
        }

        [Inject] private State m_State;

        protected override void OnUpdate()
        {
            var state = m_State.S[0];

            var oldState = Random.state;
            Random.state = state.RandomState;

            state.Cooldown -= Time.deltaTime;

            if (state.Cooldown <= 0.0f)
            {
                var settings = TwoStickBootstrap.Settings;
                var enemy = Object.Instantiate(settings.EnemyPrefab);
                //@TODO set transform
                ComputeSpawnLocation(enemy);
                state.SpawnedEnemyCount++;
                state.Cooldown = ComputeCooldown(state.SpawnedEnemyCount);
            }

            state.RandomState = Random.state;

            Random.state = oldState;
        }

        private float ComputeCooldown(int stateSpawnedEnemyCount)
        {
            return 0.15f;
        }

        private void ComputeSpawnLocation(Transform2D xform)
        {
            var settings = TwoStickBootstrap.Settings;

            float r = Random.value;
            float x0 = settings.playfield.xMin;
            float x1 = settings.playfield.xMax;
            float x = x0 + (x1 - x0) * r;

            xform.Position = new float2(x, settings.playfield.yMax);
            xform.Heading = new float2(0, 1);
        }
    }

    public class EnemyMoveSystem : ComponentSystem
    {
        struct Data
        {
            public Enemy Tag;
            public Health Health;
            public Transform2D Transform2D;
        }

        protected override void OnUpdate()
        {
            if (!TwoStickBootstrap.Settings)
                return;
            var settings = TwoStickBootstrap.Settings;
            var speed = settings.enemySpeed;
            var minY = settings.playfield.yMin;
            var maxY = settings.playfield.yMax;

            foreach (var entity in GetEntities<Data>())
            {
                var xform = entity.Transform2D;
                xform.Position.y -= speed;

                if (xform.Position.y > maxY || xform.Position.y < minY)
                {
                    entity.Health.Value = -1;
                }
            }
        }
    }

    [DisableSystemWhenEmpty]
    public class EnemyShootSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentArray<Transform2D> Transform2D;
            public ComponentArray<EnemyShootState> ShootState;
        }

        [Inject] private Data m_Data;

        public struct PlayerData
        {
            public int Length;
            public ComponentArray<Transform2D> Transform2D;
            public ComponentArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerData m_Player;

        protected override void OnUpdate()
        {
            var playerPos = m_Player.Transform2D[0].Position;

            var shotSpawnData = new List<ShotSpawnData>();

            float dt = Time.deltaTime;
            float shootRate = TwoStickBootstrap.Settings.enemyShootRate;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                var state = m_Data.ShootState[i];

                state.Cooldown -= dt;
                if (state.Cooldown <= 0.0)
                {
                    state.Cooldown = shootRate;
                    var position = m_Data.Transform2D[i].Position;

                    ShotSpawnData spawn = new ShotSpawnData()
                    {
                        Position = position,
                        Heading = math.normalize(playerPos - position),
                        Faction = TwoStickBootstrap.Settings.EnemyFaction
                    };
                    shotSpawnData.Add(spawn);
                }
            }

            // TODO: Batch
            foreach (var spawn in shotSpawnData)
            {
                ShotSpawnSystem.SpawnShot(spawn);
            }
        }
    }

}
