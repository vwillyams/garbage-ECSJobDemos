using System.Security.Cryptography;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickPureExample
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
            }

            state.RandomState = Random.state;

            m_State.S[0] = state;
            Random.state = oldState;

            // Need to do this after we're done accessing our injected arrays.
            if (spawn)
            {
                Entity e = EntityManager.CreateEntity(TwoStickBootstrap.BasicEnemyArchetype);
                EntityManager.SetComponentData(e, spawnXform);
                EntityManager.SetComponentData(e, default(Enemy));
                EntityManager.SetComponentData(e, new Health { Value = TwoStickBootstrap.Settings.enemyInitialHealth });
                EntityManager.SetComponentData(e, new EnemyShootState { Cooldown = 0.5f });
                EntityManager.SetComponentData(e, new Faction { Value = Faction.kEnemy });

                EntityManager.AddSharedComponentData(e, TwoStickBootstrap.EnemyLook);
            }
        }

        private float ComputeCooldown(int stateSpawnedEnemyCount)
        {
            return 0.15f;
        }

        private void ComputeSpawnLocation(out Transform2D xform)
        {
            var settings = TwoStickBootstrap.Settings;

            float r = Random.value;
            float x0 = settings.playfield.xMin;
            float x1 = settings.playfield.xMax;
            float x = x0 + (x1 - x0) * r;

            xform.Position = new float2(x, settings.playfield.yMax); // Y axis is positive up
            xform.Heading = new float2(0, 1);
        }
    }

    public class EnemyMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Enemy> EnemyTag;
            public ComponentDataArray<Health> Health;
            public ComponentDataArray<Transform2D> Transform2D;
        }

        [Inject] private Data m_Data;

        public struct MoveJob : IJobParallelFor
        {
            public ComponentDataArray<Health> Health;
            public ComponentDataArray<Transform2D> Transform2D;

            public float Speed;
            public float MinY;
            public float MaxY;

            public void Execute(int index)
            {
                var xform = Transform2D[index];
                xform.Position.y -= Speed;

                if (xform.Position.y > MaxY || xform.Position.y < MinY)
                {
                    Health[index] = new Health { Value = -1.0f };
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
                Health = m_Data.Health,
                Transform2D = m_Data.Transform2D,
                Speed = TwoStickBootstrap.Settings.enemySpeed,
                MinY = TwoStickBootstrap.Settings.playfield.yMin,
                MaxY = TwoStickBootstrap.Settings.playfield.yMax,
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
            float shotEnergy = TwoStickBootstrap.Settings.enemyShotEnergy;

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
                    spawn.Shot.Energy = shotEnergy;
                    spawn.Transform = m_Data.Transform2D[i];
                    spawn.Transform.Heading = math.normalize(playerPos - spawn.Transform.Position);
                    spawn.Faction = new Faction { Value = Faction.kEnemy };
                    shotLocations[shotCount++] = spawn;
                }

                m_Data.ShootState[i] = state;
            }

            // TODO: Batch
            for (int i = 0; i < shotCount; ++i)
            {
                var e = EntityManager.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype);
                EntityManager.SetComponentData(e, shotLocations[i]);
            }

            shotLocations.Dispose();
        }
    }

}
