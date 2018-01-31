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
            public ComponentDataArray<Transform2D> Transform;
            public ComponentDataArray<PlayerInput> Input;
        }

        [Inject] private Data m_Data;

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
                Transform2D xform = m_Data.Transform[index];

                var playerInput = m_Data.Input[index];

                xform.Position += dt * playerInput.Move * settings.playerMoveSpeed;

                if (playerInput.Fire)
                {
                    xform.Heading = math.normalize(playerInput.Shoot);

                    playerInput.FireCooldown = settings.playerFireCoolDown;

                    pendingShots[pendingShotCount++] = new ShotSpawnData
                    {
                        Shot = new Shot
                        {
                            Speed = settings.bulletMoveSpeed,
                            TimeToLive = settings.bulletTimeToLive,
                        },
                        Transform = xform,
                        IsPlayer = 1,
                    };
                }

                m_Data.Transform[index] = xform;
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
