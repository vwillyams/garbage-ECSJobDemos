using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms2D;
using UnityEngine;

namespace TwoStickPureExample
{
    class EnemyShootSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
            public ComponentDataArray<EnemyShootState> ShootState;
        }

        [Inject] private Data m_Data;

        public struct PlayerData
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
            [ReadOnly] public ComponentDataArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerData m_Player;

        protected override void OnUpdate()
        {
            if (m_Data.Length == 0 || m_Player.Length == 0)
                return;

            var playerPos = m_Player.Position[0].Value;

            float dt = Time.deltaTime;
            float shootRate = TwoStickBootstrap.Settings.enemyShootRate;
            float shotTtl = TwoStickBootstrap.Settings.enemyShotTimeToLive;
            float shotEnergy = TwoStickBootstrap.Settings.enemyShotEnergy;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                var state = m_Data.ShootState[i];

                state.Cooldown -= dt;
                if (state.Cooldown <= 0.0)
                {
                    state.Cooldown = shootRate;

                    ShotSpawnData spawn;
                    spawn.Shot.TimeToLive = shotTtl;
                    spawn.Shot.Energy = shotEnergy;
                    spawn.Position = m_Data.Position[i];
                    spawn.Heading = new Heading2D {Value = math.normalize(playerPos - m_Data.Position[i].Value)};
                    spawn.Faction = Factions.kEnemy;

                    PostUpdateCommands.CreateEntity(TwoStickBootstrap.ShotSpawnArchetype);
                    PostUpdateCommands.SetComponent(spawn);
                }

                m_Data.ShootState[i] = state;
            }
        }
    }
}