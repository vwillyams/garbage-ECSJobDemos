using Unity.Collections;
using Unity.ECS;
using Unity.Mathematics;
using UnityEngine.ECS;
using Unity.Transforms2D;

namespace TwoStickPureExample
{

    /// <summary>
    /// Assigns out damage from shots colliding with entities of other factions.
    /// </summary>
    class ShotDamageSystem : ComponentSystem
    {
        /// <summary>
        /// An array of entities that can take damage (players/enemies both included)
        /// </summary>
        struct ReceiverData
        {
            public int Length;
            public ComponentDataArray<Health> Health;
            [ReadOnly] public ComponentDataArray<Faction> Faction;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
        }

        [Inject] ReceiverData m_Receivers;

        /// <summary>
        /// All our shots, and the factions who fired them.
        /// </summary>
        struct ShotData
        {
            public int Length;
            public ComponentDataArray<Shot> Shot;
            [ReadOnly] public ComponentDataArray<Position2D> Position;
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

                float collisionRadius = GetCollisionRadius(settings, m_Receivers.Faction[pi].Value);
                float collisionRadiusSquared = collisionRadius * collisionRadius;

                float2 receiverPos = m_Receivers.Position[pi].position;
                int receiverFaction = m_Receivers.Faction[pi].Value;

                for (int si = 0; si < m_Shots.Length; ++si)
                {
                    if (m_Shots.Faction[si].Value != receiverFaction)
                    {
                        float2 shotPos = m_Shots.Position[si].position;
                        float2 delta = shotPos - receiverPos;
                        float distSquared = math.dot(delta, delta);
                        if (distSquared <= collisionRadiusSquared)
                        {
                            var shot = m_Shots.Shot[si];

                            damage += shot.Energy;

                            // Set the shot's time to live to zero, so it will be collected by the shot destroy system
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

        float GetCollisionRadius(TwoStickExampleSettings settings, int faction)
        {
            // This simply picks the collision radius based on whether the receiver is the player or not.
            // In a real game, this would be much more sophisticated, perhaps with a CollisionRadius component.
            return faction == Faction.kPlayer ? settings.playerCollisionRadius : settings.enemyCollisionRadius;
        }
    }
}
