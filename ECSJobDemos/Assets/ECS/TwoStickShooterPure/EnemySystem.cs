using System.Security.Cryptography;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickExample
{
    // Spawns new enemies.
    public class EnemySpawnSystem : ComponentSystem
    {

        public struct State
        {
            public int Length;
            public ComponentDataArray<EnemySpawnSystemState> S;
        }

        [Inject] private State m_State;

        protected override void OnUpdate()
        {
            if (m_State.Length == 0)
                return;

            var spawn = false;
            Transform2D spawnXform = new Transform2D();

            // This is currently a copy in-out but will be nicer when C# 7 ref returns are in
            var state = m_State.S[0];

            var oldState = Random.state;
            Random.state = state.RandomState;

            state.Cooldown -= Time.deltaTime;

            if (state.Cooldown <= 0.0f)
            {
                ComputeSpawnLocation(out spawnXform);
                state.SpawnedEnemyCount++;
                state.Cooldown = ComputeCooldown(state.SpawnedEnemyCount);
                spawn = true;
                Debug.Log($"Spawned an enemy, count so far: {state.SpawnedEnemyCount}");
            }

            state.RandomState = Random.state;

            m_State.S[0] = state;
            Random.state = oldState;

            // Need to do this after we're done accessing our injected arrays.
            if (spawn)
            {
                Entity e = EntityManager.CreateEntity(TwoStickBootstrap.BasicEnemyArchetype);
                EntityManager.SetComponent(e, spawnXform);
                EntityManager.SetComponent(e, new Enemy { Health = 1 });
                EntityManager.SetComponent(e, new EnemyShootState { Cooldown = 0.5f });
                EntityManager.AddSharedComponent(e, TwoStickBootstrap.EnemyLook);
            }
        }

        private float ComputeCooldown(int stateSpawnedEnemyCount)
        {
            return 0.75f;
        }

        private void ComputeSpawnLocation(out Transform2D xform)
        {
            float r = Random.value;
            const float x0 = -50.0f;
            const float x1 = 50.0f;
            float x = x0 + (x1 - x0) * r;

            xform.Position = new float2(x, 5);
            xform.Heading = new float2(0, 1);
        }
    }

    public class EnemyMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentDataArray<Enemy> Enemy;
            public ComponentDataArray<Transform2D> Transform2D;
        }

        [Inject] private Data m_Data;

        public struct MoveJob : IJobParallelFor
        {
            public ComponentDataArray<Enemy> Enemy;
            public ComponentDataArray<Transform2D> Transform2D;

            public float Speed;
            public float MaxY;

            public void Execute(int index)
            {
                var xform = Transform2D[index];
                xform.Position.y -= Speed;

                if (xform.Position.y > MaxY || xform.Position.y < -MaxY)
                {
                    Enemy[index] = new Enemy {Health = -1};
                }

                Transform2D[index] = xform;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!TwoStickBootstrap.Settings)
                return inputDeps;
            var moveJob = new MoveJob
            {
                Enemy = m_Data.Enemy,
                Transform2D = m_Data.Transform2D,
                Speed = TwoStickBootstrap.Settings.enemySpeed,
                MaxY = 10.0f // TODO: Represent bounds somewhere
            };

            return moveJob.Schedule(m_Data.Length, 64, inputDeps);
        }
    }

    public class EnemyShootSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Transform2D> Transform2D;
            public ComponentDataArray<EnemyShootState> ShootState;
        }

        [Inject] private Data m_Data;

        public struct PlayerData
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Transform2D> Transform2D;
            [ReadOnly] public ComponentDataArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerData m_Player;

        protected override void OnUpdate()
        {
            if (m_Data.Length == 0 || m_Player.Length == 0)
                return;

            var playerPos = m_Player.Transform2D[0].Position;

            int shotCount = 0;
            var shotLocations = new NativeArray<ShotSpawnData>(m_Data.Length, Allocator.Temp);

            float dt = Time.deltaTime;
            float shootRate = TwoStickBootstrap.Settings.enemyShootRate;
            float shotSpeed = TwoStickBootstrap.Settings.enemyShotSpeed;
            float shotTtl = TwoStickBootstrap.Settings.enemyShotTimeToLive;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                var state = m_Data.ShootState[i];

                state.Cooldown -= dt;
                if (state.Cooldown <= 0.0)
                {
                    state.Cooldown = shootRate;

                    ShotSpawnData spawn;
                    spawn.Shot.Speed = shotSpeed;
                    spawn.Shot.TimeToLive = shotTtl;
                    spawn.Transform = m_Data.Transform2D[i];
                    spawn.Transform.Heading = math.normalize(playerPos - spawn.Transform.Position);

                    shotLocations[shotCount++] = spawn;
                }

                m_Data.ShootState[i] = state;
            }

            // TODO: Batch
            for (int i = 0; i < shotCount; ++i)
            {
                var e = EntityManager.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype);
                EntityManager.SetComponent(e, shotLocations[i]);
            }

            shotLocations.Dispose();
        }
    }

    public class EnemyDestroySystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public EntityArray Entity;
            [ReadOnly] public ComponentDataArray<Enemy> Enemy;
        }

        [Inject] private Data m_Data;

        protected override void OnUpdate()
        {
            int removeCount = 0;
            var enemiesToRemove = new NativeArray<Entity>(m_Data.Length, Allocator.Temp);

            for (int i = 0; i < m_Data.Length; ++i)
            {
                if (m_Data.Enemy[i].Health <= 0)
                {
                    enemiesToRemove[removeCount++] = m_Data.Entity[i];
                }
            }

            if (removeCount > 0)
            {
                Debug.Log($"Removing {removeCount} enemies");
                EntityManager.DestroyEntity(enemiesToRemove.Slice(0, removeCount));
            }

            enemiesToRemove.Dispose();
        }
    }
}
