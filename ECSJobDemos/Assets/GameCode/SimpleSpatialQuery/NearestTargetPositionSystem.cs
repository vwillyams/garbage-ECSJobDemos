using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace UnityEngine.ECS.SimpleSpatialQuery
{
    [DisableSystemWhenEmpty]
    public class NearestTargetPositionSystem<TNearestTarget,TTarget> : JobComponentSystem
        where TNearestTarget : struct, IComponentData, ISingleValue<float3>
        where TTarget : struct, IComponentData
    {
        ComponentGroup m_TargetGroup;
        ComponentGroup m_NearestTargetPositionGroup;

        // [ComputeJobOptimization]
        struct NearestTargetPosition : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<float3> targetPositions;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public ComponentDataArray<TNearestTarget> positionNearestTargets;
            public TNearestTarget defaultNearestTarget;

            public void Execute(int index)
            {
                var position = positions[index].Value;
                var nearestPosition = targetPositions[0];
                var nearestDistance = math.lengthSquared(position-nearestPosition);
                for (int i = 1; i < targetPositions.Length; i++)
                {
                    var targetPosition = targetPositions[i];
                    var distance = math.lengthSquared(position-targetPosition);
                    var nearest = distance < nearestDistance;

                    nearestDistance = math.select(nearestDistance, distance, nearest);
                    nearestPosition = math.select(nearestPosition, targetPosition, nearest);
                }

                defaultNearestTarget.Value = nearestPosition;
                positionNearestTargets[index] = defaultNearestTarget;
            }
        }

        protected override void OnCreateManager(int capacity)
        {
            m_TargetGroup = GetComponentGroup(ComponentType.ReadOnly(typeof(TTarget)), ComponentType.ReadOnly(typeof(Position)));
            m_NearestTargetPositionGroup = GetComponentGroup(typeof(TNearestTarget), ComponentType.ReadOnly(typeof(Position)));;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // Collect Targets
            var targetPositions = m_TargetGroup.GetComponentDataArray<Position>();
            var targetPositionsCopy = new NativeArray<float3>(targetPositions.Length, Allocator.TempJob);
            var collectTargetPositionsJob = new CopyComponentData<Position,float3>()
            {
                source = targetPositions,
                results = targetPositionsCopy
            };
            var collectTargetPositionsJobHandle = collectTargetPositionsJob.Schedule(targetPositions.Length, 64, inputDeps);

            // Assign Nearest Target
            var nearestTargetPositions = m_NearestTargetPositionGroup.GetComponentDataArray<Position>();
            var nearestTargets = m_NearestTargetPositionGroup.GetComponentDataArray<TNearestTarget>();
            var nearestTargetPositionJob = new NearestTargetPosition
            {
                targetPositions = targetPositionsCopy,
                positionNearestTargets = nearestTargets,
                positions = nearestTargetPositions,
                defaultNearestTarget = new TNearestTarget()
            };

            return nearestTargetPositionJob.Schedule(nearestTargets.Length, 64, collectTargetPositionsJobHandle);
        }
    }
}
