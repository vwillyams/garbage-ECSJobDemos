using Unity.ECS;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Transforms2D;

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

            var cmds = new EntityCommandBuffer();

            var settings = TwoStickBootstrap.Settings;

            float dt = Time.deltaTime;
            for (int index = 0; index < m_Data.Length; ++index)
            {
                var position = m_Data.Position[index].Value;
                var heading = m_Data.Heading[index].Heading;

                var playerInput = m_Data.Input[index];

                position += dt * playerInput.Move * settings.playerMoveSpeed;

                if (playerInput.Fire)
                {
                    heading = math.normalize(playerInput.Shoot);

                    playerInput.FireCooldown = settings.playerFireCoolDown;

                    cmds.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype);
                    cmds.SetComponent(new ShotSpawnData
                    {
                        Shot = new Shot
                        {
                            TimeToLive = settings.bulletTimeToLive,
                            Energy = settings.playerShotEnergy,
                        },
                        Position = new Position2D{ Value = position },
                        Heading = new Heading2D{ Heading = heading },
                        Faction = new Faction { Value = Faction.kPlayer },
                    });
                }

                m_Data.Position[index] = new Position2D {Value = position};
                m_Data.Heading[index] = new Heading2D {Heading = heading};
                m_Data.Input[index] = playerInput;
            }

            cmds.Playback(EntityManager);
            cmds.Dispose();
        }
    }
}
