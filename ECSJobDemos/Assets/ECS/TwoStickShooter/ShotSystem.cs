using System;
using System.CodeDom;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;

namespace TwoStickExample
{
    public class ShotMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Shot> Shot;
            public ComponentDataArray<WorldPos> Position;
        }

        [InjectComponentGroup] private Data m_Data;

        private struct ShotMoveJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Shot> Shot;
            public ComponentDataArray<WorldPos> Position;

            public void Execute(int index)
            {
                float2 pos = Position[index].Position;
                float2 dir = Position[index].Heading;

                pos += dir * Shot[index].Speed;

                // Ref return will make this nicer.
                Position[index] = new WorldPos {Position = pos, Heading = dir};

            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveJob = new ShotMoveJob {Shot = m_Data.Shot, Position = m_Data.Position};

            return moveJob.Schedule(m_Data.Length, 64, inputDeps);
        }
    }

    public class ShotSpawnSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public EntityArray SpawnedEntities;
            [ReadOnly] public ComponentDataArray<ShotSpawnData> SpawnData;
        }

        [InjectComponentGroup] private Data m_Data;

        protected override void OnUpdate()
        {
            var em = EntityManager;
            for (int i = 0; i < m_Data.Length; ++i)
            {
                var spawnData = m_Data.SpawnData[i];
                var shotEntity = m_Data.SpawnedEntities[i];
                em.RemoveComponent<ShotSpawnData>(shotEntity);
                em.AddComponent(shotEntity, spawnData.Shot);
                em.AddComponent(shotEntity, spawnData.WorldPos);
                em.AddComponent(shotEntity, default(TransformMatrix));
                em.AddSharedComponent(shotEntity, TwoStickBootstrap.ShotLook);
            }
        }
    }

    [UpdateAfter(typeof(ShotSpawnSystem))]
    [UpdateAfter(typeof(ShotMoveSystem))]
    public class ShotDestroySystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public EntityArray Entities;
            public ComponentDataArray<Shot> Shot;
        }

        [InjectComponentGroup] private Data m_Data;

        protected override void OnUpdate()
        {
            // Handle common no-op case.
            if (m_Data.Length == 0)
                return;

            int removeCount = 0;
            var entitiesToRemove = new NativeArray<Entity>(m_Data.Length, Allocator.Temp);

            float dt = Time.deltaTime;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                Shot s = m_Data.Shot[i];
                s.TimeToLive -= dt;
                if (s.TimeToLive <= 0.0f)
                {
                    entitiesToRemove[removeCount++] = m_Data.Entities[i];
                }
                m_Data.Shot[i] = s;
            }

            if (removeCount > 0)
            {
                EntityManager.DestroyEntity(entitiesToRemove.Slice(0, removeCount));
            }

            entitiesToRemove.Dispose();
        }
    }

}
