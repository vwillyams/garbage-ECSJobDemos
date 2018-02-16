
// #define BURST_FIX_1 // #BURST-ICE when job defined inside generic
// #define BURST_FIX_2 // #BURST-ICE when job defined using generic arguments/interface

// [macton 16-Feb-2018] if BURST_FIX_1 is defined against Burst 0.1.20
// [macton 16-Feb-2018] if BURST_FIX_1 + BURST_FIX_2 is defined against Burst 0.1.20
// Burst.Compiler.IL.CompilerException: Error while processing function `System.Void UnityEngine.ECS.SimpleSpatialQuery.NearestTargetPositionSystem`2/NearestTargetPosition::Execute(System.Int32)` ---> Burst.Compiler.IL.CompilerException: Error while processing variable `UnityEngine.ECS.SimpleSpatialQuery.NearestTargetPositionSystem`2/NearestTargetProxy<TNearestTarget,TTarget> var.8.;` ---> System.NullReferenceException: Object reference not set to an instance of an object

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
    }
    
#if (!BURST_FIX_1)
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
    
    // Value is hard-cast to this data only because #BURST-ICE-GENERICS
    // [ComputeJobOptimization] #BURST-ICE-GENERICS https://gitlab.internal.unity3d.com/burst/burst/issues/9
    struct NearestTargetProxy : IComponentData
    {
        public float3 value;
    }
    
    [ComputeJobOptimization]
    struct NearestTargetPosition : IJobParallelFor
    {
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<float3> targetPositions;
        [ReadOnly] public ComponentDataArray<Position> positions;
        public ComponentDataArray<NearestTargetProxy> positionNearestTargets;

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
            positionNearestTargets[index] = new NearestTargetProxy {value = nearestPosition};
        }
    }
#endif
    
    public class NearestTargetPositionSystem<TNearestTarget,TTarget> : JobComponentSystem
        where TNearestTarget : struct, IComponentData, INearestTarget
        where TTarget : struct, IComponentData
    {
        
#if (BURST_FIX_1)
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
    
#if (!BURST_FIX_2)
        // Value is hard-cast to this data only because #BURST-ICE-GENERICS
        // [ComputeJobOptimization] #BURST-ICE-GENERICS https://gitlab.internal.unity3d.com/burst/burst/issues/9
        struct NearestTargetProxy : IComponentData
        {
            public float3 value;
        }
#endif
    
        [ComputeJobOptimization]
        struct NearestTargetPosition : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<float3> targetPositions;
            [ReadOnly] public ComponentDataArray<Position> positions;
#if BURST_FIX_2
            public ComponentDataArray<TNearestTarget> positionNearestTargets;
#else
            public ComponentDataArray<NearestTargetProxy> positionNearestTargets;
#endif

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
#if BURST_FIX_2
                positionNearestTargets[index] = new TNearestTarget {value = nearestPosition};
#else
                positionNearestTargets[index] = new NearestTargetProxy {value = nearestPosition};
#endif    
            }
        }
#endif
    
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // Collect Targets
                
            var targetGroup = EntityManager.CreateComponentGroup(ComponentType.ReadOnly(typeof(TTarget)), ComponentType.ReadOnly(typeof(Position)));
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

            var nearestTargetPositionGroup = EntityManager.CreateComponentGroup(typeof(TNearestTarget), ComponentType.ReadOnly(typeof(Position)));
            var nearestTargetPositions = nearestTargetPositionGroup.GetComponentDataArray<Position>();
            
#if BURST_FIX_2
            var nearestTargets = nearestTargetPositionGroup.GetComponentDataArray<TNearestTarget>();
#else
            // Value is hard-cast to this data only because #BURST-ICE-GENERICS
            // [ComputeJobOptimization] #BURST-ICE-GENERICS https://gitlab.internal.unity3d.com/burst/burst/issues/9
            // var nearestTargets = nearestTargetPositionGroup.GetComponentDataArray<TNearestTarget>();
            var nearestTargets = nearestTargetPositionGroup.GetComponentDataArray<NearestTargetProxy>(typeof(TNearestTarget));
#endif

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
            
            // Nothing is injected so the return value is not used
            return inputDeps;
        }
    }
}
