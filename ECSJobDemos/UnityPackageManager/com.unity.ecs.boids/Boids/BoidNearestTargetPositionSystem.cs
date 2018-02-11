using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.Boids
{
    public class BoidNearestTargetPositionSystem : JobComponentSystem
    {
        struct BoidTargetGroup
        {
            [ReadOnly] public ComponentDataArray<BoidTarget> targets;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] private BoidTargetGroup m_TargetGroup;

        struct BoidNearestTargetPositionGroup
        {
            public ComponentDataArray<BoidNearestTargetPosition> positionNearestTargets;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] private BoidNearestTargetPositionGroup m_NearestTargetPositionGroup;

        [ComputeJobOptimization]
        struct CollectTargetPositions : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float3> results;

            public void Execute(int index)
            {
                results[index] = positions[index].position;
            }
        }

        [ComputeJobOptimization]
        struct NearestTargetPosition : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<float3> targetPositions;
            public ComponentDataArray<BoidNearestTargetPosition> positionNearestTargets;
            [ReadOnly] public ComponentDataArray<Position> positions;

            public void Execute(int index)
            {
                var position = positions[index].position;
                var nearestPosition = targetPositions[0];
                var nearestDistance = math.lengthSquared(position-nearestPosition);
                for (int i = 1; i < targetPositions.Length; i++)
                {
                    var targetPosition = targetPositions[i];
                    var distance = math.lengthSquared(position-targetPosition);
                    var nearest = distance < nearestDistance;

                    nearestDistance = math.select(distance, nearestDistance, nearest);
                    nearestPosition = math.select(targetPosition, nearestPosition, nearest);
                }
                positionNearestTargets[index] = new BoidNearestTargetPosition {value = nearestPosition};
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_TargetGroup.Length == 0)
            {
                return inputDeps;
            }

            if (m_NearestTargetPositionGroup.Length == 0)
            {
                return inputDeps;
            }

            var targetPositions = new NativeArray<float3>(m_TargetGroup.Length, Allocator.TempJob);

            var collectTargetPositionsJob = new CollectTargetPositions
            {
                positions = m_TargetGroup.positions,
                results = targetPositions
            };
            var collectTargetPositionsJobHandle =
                collectTargetPositionsJob.Schedule(m_TargetGroup.Length, 64, inputDeps);

            var nearestTargetPositionJob = new NearestTargetPosition
            {
                targetPositions = targetPositions,
                positionNearestTargets = m_NearestTargetPositionGroup.positionNearestTargets,
                positions = m_NearestTargetPositionGroup.positions
            };
            var nearestTargetPositionJobHandle = nearestTargetPositionJob.Schedule(m_NearestTargetPositionGroup.Length, 64,
                collectTargetPositionsJobHandle);

            return nearestTargetPositionJobHandle;
        }
    }
}
