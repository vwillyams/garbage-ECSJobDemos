using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Msagl.Core.Layout.ProximityOverlapRemoval.StressEnergy;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickExample
{
    public class PlayerInputSystem : ComponentSystem
    {
        struct PlayerData
        {
            public int Length;

            public ComponentDataArray<PlayerInput> Input;
        }

        [InjectComponentGroup] private PlayerData m_Players;

        protected override void OnUpdate()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < m_Players.Length; ++i)
            {
                UpdatePlayerInput(i, dt);
            }
        }

        private void UpdatePlayerInput(int i, float dt)
        {
            PlayerInput pi;

            pi.Move.x = Input.GetAxis("MoveX");
            pi.Move.y = Input.GetAxis("MoveY");
            pi.Shoot.x = Input.GetAxis("ShootX");
            pi.Shoot.y = Input.GetAxis("ShootY");

            pi.FireCooldown = Mathf.Max(0.0f, m_Players.Input[i].FireCooldown - dt);

            m_Players.Input[i] = pi;
        }
    }
}
