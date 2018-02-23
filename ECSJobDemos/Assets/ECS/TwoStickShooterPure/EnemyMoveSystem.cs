using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms2D;

namespace TwoStickPureExample
{
    // Spawns new enemies.

    class EnemyMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            [ReadOnly] public ComponentDataArray<Enemy> EnemyTag;
            public ComponentDataArray<Health> Health;
            public ComponentDataArray<Position2D> Position;
        }

        [Inject] Data m_Data;

        public struct boundaryKillJob : IJobParallelFor
        {
            public ComponentDataArray<Health> Health;
            [ReadOnly] public ComponentDataArray<Position2D> Position;

            public float MinY;
            public float MaxY;

            public void Execute(int index)
            {
                var position = Position[index].Value;

                if (position.y > MaxY || position.y < MinY)
                {
                    Health[index] = new Health { Value = -1.0f };
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!TwoStickBootstrap.Settings)
                return inputDeps;
            var boundaryKillJob = new boundaryKillJob
            {
                Health = m_Data.Health,
                Position = m_Data.Position,
                MinY = TwoStickBootstrap.Settings.playfield.yMin,
                MaxY = TwoStickBootstrap.Settings.playfield.yMax,
            };

            return boundaryKillJob.Schedule(m_Data.Length, 64, inputDeps);
        }
    }
}
