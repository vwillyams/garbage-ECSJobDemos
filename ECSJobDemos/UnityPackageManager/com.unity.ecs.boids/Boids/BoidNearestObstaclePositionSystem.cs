using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.Boids
{
    public class BoidNearestObstaclePositionSystem : JobComponentSystem
    {
        struct BoidObstacleGroup
        {
            [ReadOnly] public ComponentDataArray<BoidObstacle> obstacles;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] private BoidObstacleGroup m_ObstacleGroup;

        struct BoidNearestObstaclePositionGroup
        {
            public ComponentDataArray<BoidNearestObstaclePosition> positionNearestObstacles;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] private BoidNearestObstaclePositionGroup m_NearestObstaclePositionGroup;

        [ComputeJobOptimization]
        struct CollectObstaclePositions : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float3> results;

            public void Execute(int index)
            {
                results[index] = positions[index].position;
            }
        }

        [ComputeJobOptimization]
        struct NearestObstaclePosition : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<float3> obstaclePositions;
            public ComponentDataArray<BoidNearestObstaclePosition> positionNearestObstacles;
            [ReadOnly] public ComponentDataArray<Position> positions;

            public void Execute(int index)
            {
                var position = positions[index].position;
                var nearestPosition = obstaclePositions[0];
                var nearestDistance = math.lengthSquared(position-nearestPosition);
                for (int i = 1; i < obstaclePositions.Length; i++)
                {
                    var obstaclePosition = obstaclePositions[i];
                    var distance = math.lengthSquared(position-obstaclePosition);
                    var nearest = distance < nearestDistance;

                    nearestDistance = math.select(distance, nearestDistance, nearest);
                    nearestPosition = math.select(obstaclePosition, nearestPosition, nearest);
                }
                positionNearestObstacles[index] = new BoidNearestObstaclePosition {value = nearestPosition};
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_ObstacleGroup.Length == 0)
            {
                return inputDeps;
            }

            if (m_NearestObstaclePositionGroup.Length == 0)
            {
                return inputDeps;
            }

            var obstaclePositions = new NativeArray<float3>(m_ObstacleGroup.Length, Allocator.TempJob);

            var collectObstaclePositionsJob = new CollectObstaclePositions
            {
                positions = m_ObstacleGroup.positions,
                results = obstaclePositions
            };
            var collectObstaclePositionsJobHandle =
                collectObstaclePositionsJob.Schedule(m_ObstacleGroup.Length, 64, inputDeps);

            var nearestObstaclePositionJob = new NearestObstaclePosition
            {
                obstaclePositions = obstaclePositions,
                positionNearestObstacles = m_NearestObstaclePositionGroup.positionNearestObstacles,
                positions = m_NearestObstaclePositionGroup.positions
            };
            var nearestObstaclePositionJobHandle = nearestObstaclePositionJob.Schedule(m_NearestObstaclePositionGroup.Length, 64,
                collectObstaclePositionsJobHandle);

            return nearestObstaclePositionJobHandle;
        }
    }
}
