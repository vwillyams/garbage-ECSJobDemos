using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TwoStickHybridExample
{
    public class PlayerMoveSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentArray<Transform2D> Transform;
            public ComponentArray<PlayerInput> Input;
        }

        [Inject] private Data m_Data;

        protected override void OnUpdate()
        {
            if (m_Data.Length == 0)
                return;

            var settings = TwoStickBootstrap.Settings;

            float dt = Time.deltaTime;
            var firingPlayers = new List<Transform2D>();
            for (int index = 0; index < m_Data.Length; ++index)
            {
                Transform2D xform = m_Data.Transform[index];

                var playerInput = m_Data.Input[index];

                xform.Position += dt * playerInput.Move * settings.playerMoveSpeed;

                if (playerInput.Fire)
                {
                    xform.Heading = math.normalize(playerInput.Shoot);
                    playerInput.FireCooldown = settings.playerFireCoolDown;

                    firingPlayers.Add(xform);
                }
            }

            foreach (var xform in firingPlayers)
            {
                var newShotData = new ShotSpawnData()
                {
                    Position = xform.Position,
                    Heading = xform.Heading,
                    Faction = xform.GetComponent<Faction>()
                };

                ShotSpawnSystem.SpawnShot(newShotData);
            }
        }
    }
}
