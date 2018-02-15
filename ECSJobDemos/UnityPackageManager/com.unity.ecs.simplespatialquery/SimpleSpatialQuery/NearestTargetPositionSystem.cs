using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleSpatialQuery
{
    public interface INearestTarget
    {
        float3 value { get; set; }
        Type TargetType();
    }

    struct NearestTargetPositionData : IComponentData
    {
        public float3 value;
    }
    
    public class NearestTargetPositionSystem : JobComponentSystem
    {
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
            public ComponentDataArray<NearestTargetPositionData> positionNearestTargets;
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
                positionNearestTargets[index] = new NearestTargetPositionData {value = nearestPosition};
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            List<Type> nearestTargetPositionTypes = EntityManager.GetAssignableComponentTypes(typeof(INearestTarget));
            
            for (int typeIndex = 0; typeIndex < nearestTargetPositionTypes.Count; typeIndex++)
            {
                var nearestPositionType = nearestTargetPositionTypes[typeIndex];
                var nearestPosition = Activator.CreateInstance(nearestPositionType) as INearestTarget;
                var targetType = nearestPosition.TargetType();
                
                // Collect Targets
                
                var targetGroup = EntityManager.CreateComponentGroup(ComponentType.ReadOnly(targetType), ComponentType.ReadOnly(typeof(Position)));
                var targetPositions = targetGroup.GetComponentDataArray<Position>();
                var targetPositionsCopy = new NativeArray<float3>(targetPositions.Length, Allocator.TempJob);

                var collectTargetPositionsJob = new CollectTargetPositions
                { 
                    positions = targetPositions,
                    results = targetPositionsCopy
                };
                // Nothing is injected so inputDeps is not used
                var collectTargetPositionsJobHandle =
                    collectTargetPositionsJob.Schedule(targetPositions.Length, 64, targetGroup.GetDependency());

                targetGroup.AddDependency(collectTargetPositionsJobHandle);
                targetGroup.Dispose();

                // Assign Nearest Target

                var nearestTargetPositionGroup = EntityManager.CreateComponentGroup(nearestPositionType, ComponentType.ReadOnly(typeof(Position)));
                var nearestTargetPositions = nearestTargetPositionGroup.GetComponentDataArray<Position>();
                var nearestTargets = nearestTargetPositionGroup.GetComponentDataArray<NearestTargetPositionData>(nearestPositionType);

                var nearestTargetPositionJob = new NearestTargetPosition
                {
                    targetPositions = targetPositionsCopy,
                    positionNearestTargets = nearestTargets,
                    positions = nearestTargetPositions
                };
                // This job reads targetPositionsCopy which collectTargetPositionsJobHandle writes, it is not tracked by the group since it not a component so a manual dependency is required
                var nearestTargetPositionJobHandle = nearestTargetPositionJob.Schedule(nearestTargets.Length, 64,
                    JobHandle.CombineDependencies(nearestTargetPositionGroup.GetDependency(), collectTargetPositionsJobHandle));

                nearestTargetPositionGroup.AddDependency(nearestTargetPositionJobHandle);

                nearestTargetPositionGroup.Dispose();
            }

            // Nothing is injected so the return value is not used
            return inputDeps;
        }
    }
}
