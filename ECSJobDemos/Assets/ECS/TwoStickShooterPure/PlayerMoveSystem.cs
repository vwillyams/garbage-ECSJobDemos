using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;
using UnityEngine.ECS.Transform2D;
using UnityEngine.XR.WSA;

namespace TwoStickPureExample
{
    public class PlayerMoveSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentDataArray<Position2D> Position;
            public ComponentDataArray<Heading2D> Heading;
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
                var position = m_Data.Position[index].position;
                var heading = m_Data.Heading[index].heading;

                var playerInput = m_Data.Input[index];

                position += dt * playerInput.Move * settings.playerMoveSpeed;

                if (playerInput.Fire)
                {
                    heading = math.normalize(playerInput.Shoot);

                    playerInput.FireCooldown = settings.playerFireCoolDown;

                    pendingShots[pendingShotCount++] = new ShotSpawnData
                    {
                        Shot = new Shot
                        {
                            TimeToLive = settings.bulletTimeToLive,
                            Energy = settings.playerShotEnergy,
                        },
                        Position = new Position2D{ position = position },
                        Heading = new Heading2D{ heading = heading },
                        Faction = new Faction { Value = Faction.kPlayer },
                    };
                }

                m_Data.Position[index] = new Position2D {position = position};
                m_Data.Heading[index] = new Heading2D {heading = heading};
                m_Data.Input[index] = playerInput;
            }

            if (pendingShotCount > 0)
            {
                var shotEvents = new NativeArray<Entity>(pendingShotCount, Allocator.Temp);
                EntityManager.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype, shotEvents);

                for (int i = 0; i < pendingShotCount; ++i)
                {
                    EntityManager.SetComponentData(shotEvents[i], pendingShots[i]);
                }

                shotEvents.Dispose();
            }

            pendingShots.Dispose();
        }
    }
}
