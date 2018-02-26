using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.ECS.SimpleMovement2D;

namespace TwoStickPureExample
{
    [UpdateAfter(typeof(ShotSpawnSystem))]
    [UpdateAfter(typeof(MoveForward2DSystem))]
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