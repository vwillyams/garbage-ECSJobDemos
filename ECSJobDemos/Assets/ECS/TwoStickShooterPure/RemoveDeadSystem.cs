using Unity.Collections;
using Unity.ECS;

namespace TwoStickPureExample
{
    /// <summary>
    /// This system deletes entities that have a Health component with a value less than or equal to zero.
    /// </summary>
    public class RemoveDeadSystem : ComponentSystem
    {
        private struct Data
        {
            public int Length;
            [ReadOnly] public EntityArray Entity;
            [ReadOnly] public ComponentDataArray<Health> Health;
        }

        [Inject] private Data m_Data;

        private struct PlayerCheck
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<PlayerInput> PlayerInput;
        }

        [Inject] private PlayerCheck m_PlayerCheck;

        protected override void OnUpdate()
        {
            var commands = new EntityCommandBuffer(Allocator.TempJob);

            bool playerDead = m_PlayerCheck.Length == 0;

            for (int i = 0; i < m_Data.Length; ++i)
            {
                if (m_Data.Health[i].Value <= 0.0f || playerDead)
                {
                    commands.DestroyEntity(m_Data.Entity[i]);
                }
            }

            commands.Playback(EntityManager);
            commands.Dispose();
        }
    }

}
