using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
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
            for (int i = 0; i < m_Players.Length; ++i)
            {
                UpdatePlayerInput(i);
            }
        }

        private void UpdatePlayerInput(int i)
        {
            PlayerInput pi;

            pi.Fire = Input.GetButtonDown("Fire1") ? (byte) 1 : (byte) 0;

            pi.Move.x = Input.GetAxis("Horizontal");
            pi.Move.y = Input.GetAxis("Vertical");

            // TODO: When I get a gamepad
            pi.Shoot.x = 0.0f;
            pi.Shoot.y = 1.0f;

            m_Players.Input[i] = pi;
        }
    }
}
