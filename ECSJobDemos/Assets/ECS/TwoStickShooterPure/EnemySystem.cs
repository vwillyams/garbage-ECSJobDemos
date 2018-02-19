using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS.SimpleMovement;
using Unity.Transforms2D;

namespace TwoStickPureExample
{
    // Spawns new enemies.
    class EnemySpawnSystem : ComponentSystem
    {

        public struct State
        {
            public int Length;
            public ComponentDataArray<EnemySpawnCooldown> Cooldown;
            public ComponentDataArray<EnemySpawnSystemState> S;
        }

        [Inject] State m_State;

        protected override void OnUpdate()
        {
            if (m_State.Length == 0)
                return;

            float cooldown = m_State.Cooldown[0].Value;

            cooldown = Mathf.Max(0.0f, m_State.Cooldown[0].Value - Time.deltaTime);
            bool spawn = cooldown <= 0.0f;

            if (spawn)
            {
                cooldown = ComputeCooldown();
            }

            m_State.Cooldown[0] = new EnemySpawnCooldown { Value = cooldown };

            if (spawn)
            {
                SpawnEnemy();
            }
        }

        void SpawnEnemy()
        {
            var state = m_State.S[0];
            var oldState = Random.state;
            Random.state = state.RandomState;

            float2 spawnPosition = ComputeSpawnLocation();
            state.SpawnedEnemyCount++;

            state.RandomState = Random.state;

            m_State.S[0] = state;
            Random.state = oldState;

            // Need to do this after we're done accessing our injected arrays.
            Entity e = EntityManager.CreateEntity(TwoStickBootstrap.BasicEnemyArchetype);
            EntityManager.SetComponentData(e, new Position2D { Value = spawnPosition });
            EntityManager.SetComponentData(e, new Heading2D { Heading = new float2(0.0f, -1.0f) });
            EntityManager.SetComponentData(e, default(Enemy));
            EntityManager.SetComponentData(e, new Health { Value = TwoStickBootstrap.Settings.enemyInitialHealth });
            EntityManager.SetComponentData(e, new EnemyShootState { Cooldown = 0.5f });
            EntityManager.SetComponentData(e, new Faction { Value = Faction.kEnemy });
            EntityManager.SetComponentData(e, new MoveSpeed { speed = TwoStickBootstrap.Settings.enemySpeed });
            EntityManager.AddSharedComponentData(e, TwoStickBootstrap.EnemyLook);
        }

        float ComputeCooldown()
        {
            return 0.15f;
        }

        float2 ComputeSpawnLocation()
        {
            var settings = TwoStickBootstrap.Settings;

            float r = Random.value;
            float x0 = settings.playfield.xMin;
            float x1 = settings.playfield.xMax;
            float x = x0 + (x1 - x0) * r;

            return new float2(x, settings.playfield.yMax); // Y axis is positive up
        }
    }

    class EnemyMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Enemy> EnemyTag;
            public ComponentDataArray<Health> Health;
            public ComponentDataArray<Position2D> Position;
        }

        [Inject] Data m_Data;

        public struct boundaryKillJob : IJobParallelFor
        {
            public ComponentDataArray<Health> Health;
            [ReadOnly] public ComponentDataArray<Position2D> Position;

            public float MinY;
            public float MaxY;

            public void Execute(int index)
            {
                var position = Position[index].Value;

                if (position.y > MaxY || position.y < MinY)
                {
                    Health[index] = new Health { Value = -1.0f };
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!TwoStickBootstrap.Settings)
                return inputDeps;
            var boundaryKillJob = new boundaryKillJob
            {
                Health = m_Data.Health,
                Position = m_Data.Position,
                MinY = TwoStickBootstrap.Settings.playfield.yMin,
                MaxY = TwoStickBootstrap.Settings.playfield.yMax,
            };

            return boundaryKillJob.Schedule(m_Data.Length, 64, inputDeps);
        }
    }

    class EnemyShootSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
            public ComponentDataArray<EnemyShootState> ShootState;
        }

        [Inject] private Data m_Data;

        public struct PlayerData
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
            [ReadOnly] public ComponentDataArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerData m_Player;

        protected override void OnUpdate()
        {
            if (m_Data.Length == 0 || m_Player.Length == 0)
                return;

            var playerPos = m_Player.Position[0].Value;

            var cmds = new EntityCommandBuffer(Allocator.TempJob);

            float dt = Time.deltaTime;
            float shootRate = TwoStickBootstrap.Settings.enemyShootRate;
            float shotTtl = TwoStickBootstrap.Settings.enemyShotTimeToLive;
            float shotEnergy = TwoStickBootstrap.Settings.enemyShotEnergy;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                var state = m_Data.ShootState[i];

                state.Cooldown -= dt;
                if (state.Cooldown <= 0.0)
                {
                    state.Cooldown = shootRate;

                    ShotSpawnData spawn;
                    spawn.Shot.TimeToLive = shotTtl;
                    spawn.Shot.Energy = shotEnergy;
                    spawn.Position = m_Data.Position[i];
                    spawn.Heading = new Heading2D {Heading = math.normalize(playerPos - m_Data.Position[i].Value)};
                    spawn.Faction = new Faction { Value = Faction.kEnemy };

                    cmds.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype);
                    cmds.SetComponent(spawn);
                }

                m_Data.ShootState[i] = state;
            }

            cmds.Playback(EntityManager);
            cmds.Dispose();
        }
    }

}
