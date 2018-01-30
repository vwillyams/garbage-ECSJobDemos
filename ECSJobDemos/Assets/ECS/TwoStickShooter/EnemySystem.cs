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
        // TODO: Call out that this is better than storing state in the system, because it can support things like replay.
        public struct EnemySpawnSystemState : IComponentData
        {
            public int SpawnedEnemyCount;
            public float Cooldown;
            public Random.State RandomState;
        }

        public struct State
        {
            public int Length;
            public ComponentDataArray<EnemySpawnSystemState> S;
        }

        [InjectComponentGroup] private State m_State;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            var arch = EntityManager.CreateArchetype(typeof(EnemySpawnSystemState));
            var stateEntity = EntityManager.CreateEntity(arch);
            var oldState = Random.state;
            Random.InitState(0xaf77);
            EntityManager.SetComponent(stateEntity, new EnemySpawnSystemState
            {
                Cooldown = 0.0f,
                SpawnedEnemyCount = 0,
                RandomState = Random.state
            });
            Random.state = oldState;
        }

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
                EntityManager.SetComponent(e, new Enemy {Health = 1});
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

        [InjectComponentGroup] private Data m_Data;

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

    public class EnemyDestroySystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public EntityArray Entity;
            [ReadOnly] public ComponentDataArray<Enemy> Enemy;
        }

        [InjectComponentGroup] private Data m_Data;

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
