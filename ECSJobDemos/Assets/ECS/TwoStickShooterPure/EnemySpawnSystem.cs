using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms2D;
using UnityEngine;
using UnityEngine.ECS.SimpleMovement;

namespace TwoStickPureExample
{
    class EnemySpawnSystem : ComponentSystem
    {
        struct State
        {
            public int Length;
            public ComponentDataArray<EnemySpawnCooldown> Cooldown;
            public ComponentDataArray<EnemySpawnSystemState> S;
        }

        [Inject] State m_State;

        public static void SetupComponentData(EntityManager mgr)
        {
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
        }


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
            EntityManager.SetComponentData(e, new Heading2D { Value = new float2(0.0f, -1.0f) });
            EntityManager.SetComponentData(e, default(Enemy));
            EntityManager.SetComponentData(e, new Health { Value = TwoStickBootstrap.Settings.enemyInitialHealth });
            EntityManager.SetComponentData(e, new EnemyShootState { Cooldown = 0.5f });
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
}