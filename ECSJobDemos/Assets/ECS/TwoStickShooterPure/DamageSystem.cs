using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.UI;

namespace TwoStickExample
{

    public class PlayerDamageSystem : ComponentSystem
    {
        public struct ReceiverData
        {
            public int Length;
            public ComponentDataArray<Health> Health;
            [ReadOnly] public ComponentDataArray<Faction> Faction;
            [ReadOnly] public ComponentDataArray<Transform2D> Transform2D;
        }

        [Inject] ReceiverData m_Receivers;

        public struct ShotData
        {
            public int Length;
            public ComponentDataArray<Shot> Shot;
            [ReadOnly] public ComponentDataArray<Transform2D> Transform2D;
            [ReadOnly] public ComponentDataArray<Faction> Faction;
        }
        [Inject] ShotData m_Shots;

        protected override void OnUpdate()
        {
            if (0 == m_Receivers.Length || 0 == m_Shots.Length)
                return;

            var settings = TwoStickBootstrap.Settings;

            for (int pi = 0; pi < m_Receivers.Length; ++pi)
            {
                float damage = 0.0f;

                float collisionRadius = m_Receivers.Faction[pi].Value == Faction.kPlayer ? settings.playerCollisionRadius : settings.enemyCollisionRadius;
                float collisionRadiusSquared = collisionRadius * collisionRadius;

                float2 receiverPos = m_Receivers.Transform2D[pi].Position;
                int receiverFaction = m_Receivers.Faction[pi].Value;

                for (int si = 0; si < m_Shots.Length; ++si)
                {
                    if (m_Shots.Faction[si].Value != receiverFaction)
                    {
                        float2 shotPos = m_Shots.Transform2D[si].Position;
                        float2 delta = shotPos - receiverPos;
                        float distSquared = math.dot(delta, delta);
                        if (distSquared <= collisionRadiusSquared)
                        {
                            var shot = m_Shots.Shot[si];

                            damage += shot.Energy;

                            shot.TimeToLive = 0.0f;
                            
                            m_Shots.Shot[si] = shot;
                        }
                    }
                }

                var h = m_Receivers.Health[pi];
                h.Value = math.max(h.Value - damage, 0.0f);
                m_Receivers.Health[pi] = h;
            }
        }
    }

    public class UpdatePlayerHUD : ComponentSystem
    {
        public struct PlayerData
        {
            public int Length;
            [ReadOnly] public EntityArray Entity;
            [ReadOnly] public ComponentDataArray<PlayerInput> Input;
            [ReadOnly] public ComponentDataArray<Health> Health;
        }

        [Inject] PlayerData m_Players;

        private int m_CachedValue = Int32.MinValue;

        protected override void OnUpdate()
        {
            int displayedHealth = 0;

            if (m_Players.Length > 0)
            {
                displayedHealth = (int)m_Players.Health[0].Value;
            }

            if (m_CachedValue != displayedHealth)
            {
                Text t = GameObject.Find("HealthText")?.GetComponent<Text>();
                if (t != null)
                {
                    if (displayedHealth > 0)
                        t.text = $"HEALTH: {displayedHealth}";
                    else
                        t.text = "GAME OVER";
                }

                m_CachedValue = displayedHealth;
            }
        }
    }
}
