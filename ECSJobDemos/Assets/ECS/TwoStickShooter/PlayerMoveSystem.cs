using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.XR.WSA;

namespace TwoStickExample
{
    public class PlayerMoveSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentDataArray<WorldPos> Position;
            public ComponentDataArray<PlayerInput> Input;
        }

        [InjectComponentGroup] private Data m_Data;

        protected override void OnUpdate()
        {
            if (m_Data.Length == 0)
                return;

            int pendingShotCount = 0;
            var pendingShots = new NativeArray<ShotSpawnData>(m_Data.Length, Allocator.Temp);

            float dt = Time.deltaTime;
            for (int index = 0; index < m_Data.Length; ++index)
            {
                WorldPos pos = m_Data.Position[index];

                var playerInput = m_Data.Input[index];

                pos.Position += dt * playerInput.Move;
                pos.Heading = playerInput.Shoot;

                m_Data.Position[index] = pos;

                if (playerInput.FireCooldown <= 0.0)
                {
                    if (playerInput.Fire != 0)
                    {
                        // TODO: Setting
                        playerInput.FireCooldown = 1.0f;

                        pendingShots[pendingShotCount++] = new ShotSpawnData
                        {
                            // TODO: Settings
                            Shot = new Shot { Speed = 50.0f, TimeToLive = 1.0f },
                            WorldPos = pos,
                        };
                    }
                }

                m_Data.Input[index] = playerInput;
            }

            if (pendingShotCount > 0)
            {
                var shotEvents = new NativeArray<Entity>(pendingShotCount, Allocator.Temp);
                EntityManager.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype, shotEvents);

                for (int i = 0; i < pendingShotCount; ++i)
                {
                    EntityManager.SetComponent(shotEvents[i], pendingShots[i]);
                }

                shotEvents.Dispose();
            }

            pendingShots.Dispose();
        }
    }
}
