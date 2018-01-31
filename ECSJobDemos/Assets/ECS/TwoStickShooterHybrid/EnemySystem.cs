using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{
    // Spawns new enemies.
    public class EnemySpawnSystem : ComponentSystem
    {

        public struct State
        {
            public int Length;
            public ComponentArray<EnemySpawnSystemState> S;
        }

        [InjectComponentGroup] private State m_State;

        protected override void OnUpdate()
        {
            if (m_State.Length == 0)
                return;

            var state = m_State.S[0];

            var oldState = Random.state;
            Random.state = state.RandomState;

            state.Cooldown -= Time.deltaTime;

            if (state.Cooldown <= 0.0f)
            {
                var settings = TwoStickBootstrap.Settings;
                var enemy = Object.Instantiate(settings.EnemyPrefab);
                //@TODO set transform
                enemy.Health = 1;
                ComputeSpawnLocation(enemy.GetComponent<Transform2D>());
                state.SpawnedEnemyCount++;
                state.Cooldown = ComputeCooldown(state.SpawnedEnemyCount);
                Debug.Log($"Spawned an enemy, count so far: {state.SpawnedEnemyCount}");
            }

            state.RandomState = Random.state;
            
            Random.state = oldState;
        }

        private float ComputeCooldown(int stateSpawnedEnemyCount)
        {
            return 0.75f;
        }

        private void ComputeSpawnLocation(Transform2D xform)
        {
            float r = Random.value;
            const float x0 = -50.0f;
            const float x1 = 50.0f;
            float x = x0 + (x1 - x0) * r;

            xform.Position = new float2(x, 5);
            xform.Heading = new float2(0, 1);
        }
    }

    public class EnemyMoveSystem : ComponentSystem
    {
        struct Data
        {
            public Enemy Enemy;
            public Transform2D Transform2D;
        }

        protected override void OnUpdate()
        {
            if (!TwoStickBootstrap.Settings)
                return;
            var speed = TwoStickBootstrap.Settings.enemySpeed;
            var maxY = 10.0f; // TODO: Represent bounds somewhere

            foreach (var entity in GetEntities<Data>())
            {
                var xform = entity.Transform2D;
                xform.Position.y -= speed;

                if (xform.Position.y > maxY || xform.Position.y < -maxY)
                {
                    entity.Enemy.Health = -1;
                }
            }
        }
    }

    public class EnemyDestroySystem : ComponentSystem
    {
        public struct Data
        {
            [ReadOnly] public Enemy Enemy;
        }

        protected override void OnUpdate()
        {
            var toDestroy = new List<GameObject>();
            foreach (var entity in GetEntities<Data>())
            {
                if (entity.Enemy.Health <= 0)
                {
                    toDestroy.Add(entity.Enemy.gameObject);
                }
            }

            foreach (var go in toDestroy)
            {
                Object.Destroy(go);
            }
        }
    }
}
