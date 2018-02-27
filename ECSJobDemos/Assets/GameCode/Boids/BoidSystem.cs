using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleRotation;
using Unity.Transforms;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    public class BoidSystem : JobComponentSystem
    {
        ComponentGroup m_MainGroup;
        List<Boid> m_UniqueTypes = new List<Boid>(10);

        [ComputeJobOptimization]
        struct HashBoidLocations : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<int> positionHashes;
            public float cellRadius;

            public void Execute(int index)
            {
                var hash = GridHash.Hash(positions[index].Value, cellRadius);
                positionHashes[index] = hash;
            }
        }

        [ComputeJobOptimization]
        struct SeparationAndAlignmentSteer : IJobParallelFor
        {
            [ReadOnly] public NativeArraySharedValues<int> cells;
            [ReadOnly] public Boid settings;
            [ReadOnly] public NativeArray<float3> positions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> positionHashes;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> headings;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellsBuffer;
            public NativeArray<float3> alignmentResults;
            public NativeArray<float3> separationResults;

            public void Execute(int index)
            {
                var position = positions[index];
                var forward = headings[index];
                var separationSteering = new float3(0);
                var averageHeading = new float3(0);
                var neighbors = cells.GetSharedValueIndicesBySourceIndex(index);
                
                for (int i=0;i<neighbors.Length;i++)
                {
                    var neighborIndex = neighbors[i];
                    var otherPosition = positions[neighborIndex];
                    var otherForward  = headings[neighborIndex];
                    var otherAvoid    = position - otherPosition;

                    separationSteering += otherAvoid;
                    averageHeading += otherForward;
                }

                separationResults[index] = separationSteering;
                alignmentResults[index] = (averageHeading / neighbors.Length) - forward;
            }
        }

        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            public ComponentDataArray<Heading> headings;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public ComponentDataArray<BoidNearestTargetPosition> nearestTargetPositions;
            [ReadOnly] public ComponentDataArray<BoidNearestObstaclePosition> nearestObstaclePositions;
            [ReadOnly] public Boid settings;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> alignmentSteering;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> separationSteering;
            public float dt;

            public void Execute(int index)
            {
                var position = positions[index];
                var forward = headings[index].Value;
                var nearestObstaclePosition = nearestObstaclePositions[index].Value;
                var obstacleSteering = (position - nearestObstaclePosition);
                var avoidObstacle = (math.length(obstacleSteering) < settings.obstacleAversionDistance);
                var s0 = settings.alignmentWeight * math_experimental.normalizeSafe(alignmentSteering[index]);
                var s1 = settings.separationWeight * math_experimental.normalizeSafe(separationSteering[index]);
                var s2 = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPositions[index].Value - position);
                var normalHeading = math_experimental.normalizeSafe(s0 + s1 + s2);
                var avoidObstacleHeading = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                var s5 = math.select(normalHeading,avoidObstacleHeading,avoidObstacle);
                var steer = math_experimental.normalizeSafe(forward + dt*(s5-forward));
                headings[index] = new Heading { Value = steer };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(m_UniqueTypes);

            for (int typeIndex = 0; typeIndex < m_UniqueTypes.Count; typeIndex++)
            {
                var settings = m_UniqueTypes[typeIndex];
                var group = m_MainGroup.GetVariation(settings);
                var positions = group.GetComponentDataArray<Position>();
                var nearestObstaclePositions = group.GetComponentDataArray<BoidNearestObstaclePosition>();
                var nearestTargetPositions = group.GetComponentDataArray<BoidNearestTargetPosition>();
                var headings = group.GetComponentDataArray<Heading>();

                var positionHashes = new NativeArray<int>(positions.Length, Allocator.TempJob);
                var hashBoidLocationsJob = new HashBoidLocations
                {
                    positions = positions,
                    positionHashes = positionHashes,
                    cellRadius = settings.cellRadius
                };

                var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(positions.Length, 64, inputDeps);
                var cells = new NativeArraySharedValues<int>(positionHashes,Allocator.TempJob);
                var sharedHashesJobHandle = cells.Schedule(hashBoidLocationsJobHandle);

                var copyPositionsResults = new NativeArray<float3>(positions.Length, Allocator.TempJob);
                var copyPositionsJob = new CopyComponentData<Position,float3>
                {
                    source = positions,
                    results = copyPositionsResults
                };
                var copyPositionsJobHandle = copyPositionsJob.Schedule(positions.Length, 64, inputDeps);

                var copyHeadingsResults = new NativeArray<float3>(positions.Length, Allocator.TempJob);
                var copyHeadingsJob = new CopyComponentData<Heading,float3>()
                {
                    source = headings,
                    results = copyHeadingsResults
                };
                var copyHeadingsJobHandle = copyHeadingsJob.Schedule(positions.Length, 64, inputDeps);

                var separationResults = new NativeArray<float3>(positions.Length, Allocator.TempJob);
                var alignmentResults = new NativeArray<float3>(positions.Length, Allocator.TempJob);
                var separationAndAlignmentSteerJob = new SeparationAndAlignmentSteer
                {
                    positions = copyPositionsResults,
                    headings = copyHeadingsResults,
                    cells = cells,
                    settings = settings,
                    positionHashes = positionHashes,
                    separationResults = separationResults,
                    alignmentResults = alignmentResults,
                    cellsBuffer = cells.GetBuffer()
                };
                var separationAndAlignmentSteerJobHandle = separationAndAlignmentSteerJob.Schedule(positions.Length, 64,
                    JobHandle.CombineDependencies(sharedHashesJobHandle, copyHeadingsJobHandle, copyPositionsJobHandle));

                var steerJob = new Steer
                {
                    positions = copyPositionsResults,
                    headings = headings,
                    nearestTargetPositions = nearestTargetPositions,
                    nearestObstaclePositions = nearestObstaclePositions,
                    settings = settings,
                    alignmentSteering = alignmentResults,
                    separationSteering = separationResults,
                    dt = Time.deltaTime
                };

                inputDeps = steerJob.Schedule(positions.Length, 64, separationAndAlignmentSteerJobHandle);
                group.Dispose();
            }
            m_UniqueTypes.Clear();

            // The return value only applies to jobs working with injected components
            return inputDeps;
        }

        protected override void OnCreateManager(int capacity)
        {
            m_MainGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(Boid)),
                ComponentType.ReadOnly(typeof(Position)),
                ComponentType.ReadOnly(typeof(BoidNearestObstaclePosition)),
                ComponentType.ReadOnly(typeof(BoidNearestTargetPosition)),
                typeof(Heading));
        }
    }
}
