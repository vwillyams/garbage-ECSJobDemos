using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Msagl.Core.Layout.ProximityOverlapRemoval.StressEnergy;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{
    public class PlayerInputSystem : ComponentSystem
    {
        struct PlayerData
        {

            public PlayerInput Input;
        }

        protected override void OnUpdate()
        {
            float dt = Time.deltaTime;

            foreach (var entity in GetEntities<PlayerData>())
            {
                var pi = entity.Input;

                pi.Move.x = Input.GetAxis("Horizontal");
                pi.Move.y = Input.GetAxis("Vertical");
                pi.Shoot.x = Input.GetAxis("ShootX");
                pi.Shoot.y = Input.GetAxis("ShootY");

                pi.FireCooldown = Mathf.Max(0.0f, pi.FireCooldown - dt);
            }
        }
    }
}
