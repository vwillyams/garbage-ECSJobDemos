﻿using System;
using System.CodeDom;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;
using UnityEngine.ECS.Transform2D;

namespace TwoStickPureExample
{
    public class ShotMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Shot> Shot;
            [ReadOnly] public ComponentDataArray<Heading2D> Heading;
            public ComponentDataArray<Position2D> Position;
        }

        [Inject] private Data m_Data;

        private struct ShotMoveJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Shot> Shot;
            [ReadOnly] public ComponentDataArray<Heading2D> Heading;
            public ComponentDataArray<Position2D> Position;

            public void Execute(int index)
            {
                float2 pos = Position[index].position;
                float2 dir = Heading[index].heading;

                pos += dir * Shot[index].Speed;

                // Ref return will make this nicer.

                Position[index] = new Position2D {position = pos};
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveJob = new ShotMoveJob {Shot = m_Data.Shot, Position = m_Data.Position, Heading = m_Data.Heading};

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

        [Inject] private Data m_Data;

        protected override void OnUpdate()
        {
            var em = EntityManager;

            // Need to copy the data out so we can spawn without invalidating these arrays.
            var entities = new NativeArray<Entity>(m_Data.Length, Allocator.Temp);
            var spawnData = new NativeArray<ShotSpawnData>(m_Data.Length, Allocator.Temp);
            m_Data.SpawnedEntities.CopyTo(entities);
            m_Data.SpawnData.CopyTo(spawnData);

            for (int i = 0; i < m_Data.Length; ++i)
            {
                var sd = spawnData[i];
                var shotEntity = entities[i];
                em.RemoveComponent<ShotSpawnData>(shotEntity);
                em.AddComponentData(shotEntity, sd.Shot);
                em.AddComponentData(shotEntity, sd.Position);
                em.AddComponentData(shotEntity, sd.Heading);
                em.AddComponentData(shotEntity, sd.Faction);
                em.AddComponentData(shotEntity, default(TransformMatrix));
                em.AddSharedComponentData(shotEntity, sd.Faction.Value == Faction.kPlayer ? TwoStickBootstrap.PlayerShotLook : TwoStickBootstrap.EnemyShotLook);
            }

            spawnData.Dispose();
            entities.Dispose();
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

        [Inject] private Data m_Data;

        private struct PlayerCheck
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerCheck m_PlayerCheck;

        protected override void OnUpdate()
        {
            // Handle common no-op case.
            if (m_Data.Length == 0)
                return;

            bool playerDead = m_PlayerCheck.Length == 0;

            int removeCount = 0;
            var entitiesToRemove = new NativeArray<Entity>(m_Data.Length, Allocator.Temp);

            float dt = Time.deltaTime;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                Shot s = m_Data.Shot[i];
                s.TimeToLive -= dt;
                if (s.TimeToLive <= 0.0f || playerDead)
                {
                    entitiesToRemove[removeCount++] = m_Data.Entities[i];
                }
                m_Data.Shot[i] = s;
            }

            if (removeCount > 0)
            {
                for (int i = 0; i < removeCount; ++i)
                {
                    EntityManager.DestroyEntity(entitiesToRemove[i]);
                }
                //EntityManager.DestroyEntity(entitiesToRemove.Slice(0, removeCount));
            }

            entitiesToRemove.Dispose();
        }
    }

}
