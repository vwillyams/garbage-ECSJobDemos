using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Boo.Lang.Environments;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.XR.WSA;

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

        [InjectComponentGroup] private Data m_Data;

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
                    firingPlayers.Add(xform);
                    playerInput.FireCooldown = settings.playerFireCoolDown;
                }
            }

            foreach (var xform in firingPlayers)
            {
                var playerInput = xform.GetComponent<PlayerInput>();
                xform.Heading = math.normalize(playerInput.Shoot);
                    
                var newShot = Object.Instantiate(settings.ShotPrefab);
                newShot.Speed = settings.bulletMoveSpeed;
                newShot.TimeToLive = settings.bulletTimeToLive;
                    
                var shotXform = newShot.GetComponent<Transform2D>();
                shotXform.Position = xform.Position;
                shotXform.Heading = xform.Heading;
            }
        }
    }
}
