using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
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

            var settings = TwoStickBootstrap.Settings;

            float dt = Time.deltaTime;
            for (int index = 0; index < m_Data.Length; ++index)
            {
                WorldPos pos = m_Data.Position[index];

                var playerInput = m_Data.Input[index];

                pos.Position += dt * playerInput.Move * settings.playerMoveSpeed;

                if (playerInput.Fire)
                {
                    pos.Heading = math.normalize(playerInput.Shoot);

                    playerInput.FireCooldown = settings.playerFireCoolDown;

                    pendingShots[pendingShotCount++] = new ShotSpawnData
                    {
                        Shot = new Shot
                        {
                            Speed = settings.bulletMoveSpeed,
                            TimeToLive = settings.bulletTimeToLive,
                        },
                        WorldPos = pos,
                    };
                }

                m_Data.Position[index] = pos;
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
