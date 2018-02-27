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
        // #todo Should be Allocator.TempJpb once NativeMultiHashMap can DeallocateOnJobCompletion
        List<NativeMultiHashMap<int, int>> m_Cells;
        ComponentGroup m_MainGroup;
        List<Boid> m_UniqueTypes = new List<Boid>(10);

        [ComputeJobOptimization]
        struct HashBoidLocations : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeMultiHashMap<int, int>.Concurrent cells;
            public NativeArray<int> positionHashes;
            public float cellRadius;

            public void Execute(int index)
            {
                var hash = GridHash.Hash(positions[index].Value, cellRadius);
                positionHashes[index] = hash;
                cells.Add(hash, index);
            }
        }

        [ComputeJobOptimization]
        struct SeparationAndAlignmentSteer : IJobParallelFor
        {
            [ReadOnly] public NativeMultiHashMap<int, int> cells;
            [ReadOnly] public Boid settings;
            [ReadOnly] public NativeArray<float3> positions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> positionHashes;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> headings;
            public NativeArray<float3> alignmentResults;
            public NativeArray<float3> separationResults;

            public void Execute(int index)
            {
                var position = positions[index];
                var forward = headings[index];

                var separationSteering = new float3(0);
                var averageHeading = new float3(0);
                var hash = positionHashes[index];

                int i;
                NativeMultiHashMapIterator<int> iterator;
                bool found = cells.TryGetFirstValue(hash, out i, out iterator);
                int neighbors = 0;
                while (found)
                {
                    var otherPosition = positions[i];
                    var otherForward  = headings[i];
                    var otherAvoid    = position - otherPosition;

                    separationSteering += otherAvoid;
                    averageHeading += otherForward;
                    neighbors++;
                        
                    found = cells.TryGetNextValue(out i, ref iterator);
                }

                separationResults[index] = separationSteering;
                alignmentResults[index] = (averageHeading / neighbors) - forward;
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

                if (typeIndex > m_Cells.Count - 1)
                {
                    m_Cells.Add(new NativeMultiHashMap<int, int>(positions.Length, Allocator.Persistent));
                }
                
                var cells = m_Cells[typeIndex];
                cells.Capacity = math.max(m_Cells.Capacity, positions.Length);
                cells.Clear();
                m_Cells[typeIndex] = cells;

                var positionHashes = new NativeArray<int>(positions.Length, Allocator.TempJob);
                var hashBoidLocationsJob = new HashBoidLocations
                {
                    positions = positions,
                    positionHashes = positionHashes,
                    cells = cells,
                    cellRadius = settings.cellRadius
                };

                var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(positions.Length, 64, inputDeps);

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
                };
                var separationAndAlignmentSteerJobHandle = separationAndAlignmentSteerJob.Schedule(positions.Length, 64,
                    JobHandle.CombineDependencies(hashBoidLocationsJobHandle, copyHeadingsJobHandle,
                        copyPositionsJobHandle));

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
            m_Cells = new List<NativeMultiHashMap<int, int>>();
            m_MainGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(Boid)),
                ComponentType.ReadOnly(typeof(Position)),
                ComponentType.ReadOnly(typeof(BoidNearestObstaclePosition)),
                ComponentType.ReadOnly(typeof(BoidNearestTargetPosition)),
                typeof(Heading));
        }

        protected override void OnDestroyManager()
        {
            for (int i = 0; i < m_Cells.Count; i++)
            {
                m_Cells[i].Dispose();
            }
        }
    }
}
