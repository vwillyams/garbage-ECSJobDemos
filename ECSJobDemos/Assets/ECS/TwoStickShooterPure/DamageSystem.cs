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
        public struct PlayerData
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<PlayerInput> Input;
            [ReadOnly] public ComponentDataArray<Transform2D> Transform2D;
            public ComponentDataArray<Health> Health;
        }

        [Inject] PlayerData m_Players;

        public struct ShotData
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Shot> Shot;
            [ReadOnly] public ComponentDataArray<Transform2D> Transform2D;
            [ReadOnly] public ComponentDataArray<Faction> Faction;
        }
        [Inject] ShotData m_Shots;

        protected override void OnUpdate()
        {
            if (0 == m_Players.Length || 0 == m_Shots.Length)
                return;

            float collisionRadius = TwoStickBootstrap.Settings.playerCollisionRadius;
            float collisionRadiusSquared = collisionRadius * collisionRadius;

            for (int pi = 0; pi < m_Players.Length; ++pi)
            {
                float damage = 0.0f;

                float2 playerPos = m_Players.Transform2D[pi].Position;

                for (int si = 0; si < m_Shots.Length; ++si)
                {
                    if (m_Shots.Faction[si].Value == Faction.kEnemy)
                    {
                        float2 shotPos = m_Shots.Transform2D[si].Position;
                        float2 delta = shotPos - playerPos;
                        float distSquared = math.dot(delta, delta);
                        if (distSquared <= collisionRadiusSquared)
                        {
                            damage += m_Shots.Shot[si].Energy;
                        }
                    }
                }

                var h = m_Players.Health[pi];
                h.Value = math.max(h.Value - damage, 0.0f);
                m_Players.Health[pi] = h;
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
            if (m_Players.Length == 0)
                return;

            int displayedHealth = (int) m_Players.Health[0].Value;

            if (m_CachedValue != displayedHealth)
            {
                Text t = GameObject.Find("HealthText")?.GetComponent<Text>();
                if (t != null)
                {
                    t.text = $"HEALTH: {displayedHealth}";
                }

                m_CachedValue = displayedHealth;

                if (displayedHealth == 0)
                {
                    EntityManager.DestroyEntity(m_Players.Entity[0]);

                    if (t != null)
                    {
                        t.text = "GAME OVER";
                    }
                }
            }
        }
    }
}
