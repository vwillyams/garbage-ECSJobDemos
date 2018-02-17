using System.Collections.Generic;
using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    public class BoidSystem : JobComponentSystem
    {
        // #todo Should be Allocator.TempJpb once NativeMultiHashMap can DeallocateOnJobCompletion
        List<NativeMultiHashMap<int, int>> 	 m_Cells; 
        
        NativeArray<int3> 					 m_CellOffsetsTable;
        NativeArray<float>                   m_Bias;

        [ComputeJobOptimization]
        struct HashBoidLocations : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeMultiHashMap<int, int>.Concurrent cells;
            public float 				     			   cellRadius;

            public void Execute(int index)
            {
                var hash = GridHash.Hash(positions[index].position, cellRadius);
                cells.Add(hash, index);
            }
        }

        [ComputeJobOptimization]
        struct CopyPosition : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<float3> results;

            public void Execute(int index)
            {
                results[index] = positions[index].position;
            }
        }

        [ComputeJobOptimization]
        struct CopyHeading : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Heading> headings;
            public NativeArray<float3> results;

            public void Execute(int index)
            {
                results[index] = headings[index].value;
            }
        }

        [ComputeJobOptimization]
        struct SeparationAndAlignmentSteer : IJobParallelFor
        {
            [ReadOnly] public NativeMultiHashMap<int, int> cells;
            [ReadOnly] public NativeArray<int3> cellOffsetsTable;
            [ReadOnly] public NativeArray<float> bias;
            [ReadOnly] public Boid settings;
            [ReadOnly] public NativeArray<float3> positions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> headings;
            public NativeArray<float3> alignmentResults;
            public NativeArray<float3> separationResults;

            public void Execute(int index)
            {
                var position = positions[index];
                var forward = headings[index];

                var separationSteering = new float3(0);
                var alignmentSteering = new float3(0);

                int hash;
                int3 gridPos = GridHash.Quantize(position, settings.cellRadius);
                for (int oi = 0; oi < 7; oi++)
                {
                    var gridOffset = cellOffsetsTable[oi];

                    hash = GridHash.Hash(gridPos + gridOffset);
                    int i;

                    NativeMultiHashMapIterator<int> iterator;
                    bool found = cells.TryGetFirstValue(hash, out i, out iterator);
                    int neighbors = 0;
                    while (found)
                    {
                        if (i == index)
                        {
                            found = cells.TryGetNextValue(out i, ref iterator);
                            continue;
                        }
                        neighbors++;

                        var otherPosition = positions[i];
                        var otherForward = headings[i];

                        // add in steering contribution
                        // (opposite of the offset direction, divided once by distance
                        // to normalize, divided another time to get 1/d falloff)
                        var offset = otherPosition - (position + forward * bias[index&1023] );

                        var distanceSquared = math.lengthSquared(offset);
                        separationSteering += (offset / -distanceSquared);

                        // accumulate sum of neighbor's heading
                        alignmentSteering += otherForward;

                        found = cells.TryGetNextValue(out i, ref iterator);
                    }
                }

                separationResults[index] = math_experimental.normalizeSafe(separationSteering);
                alignmentResults[index] = math_experimental.normalizeSafe(alignmentSteering);
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
            [ReadOnly] public NativeArray<float> bias;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> alignmentSteering;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> separationSteering;
            public float dt;

            public void Execute(int index)
            {
                var position = positions[index];
                var forward = headings[index].value;
                var targetSteering = math_experimental.normalizeSafe(nearestTargetPositions[index].value - position);

                var steer = (alignmentSteering[index] * settings.alignmentWeight) +
                            (separationSteering[index] * settings.separationWeight) +
                            (targetSteering * settings.targetWeight);
                
                {
                    var obstaclePosition = nearestObstaclePositions[index].value;
                    var obstacleRadius = 10.0f;
                    var obstacleDelta1 = obstaclePosition - position;
                    var dist = math.length(obstacleDelta1);
                    var obs1Dir = obstacleDelta1 / dist;
                    var a = dist - obstacleRadius;
                    if (a < 0)
                        a = 0;
                    var f = a / settings.obstacleAversionDistance;
                    
                    steer = steer + (-obs1Dir - steer) * (1 - f) * settings.obstacleWeight;
                    steer = math_experimental.normalizeSafe(steer);
                }

                math_experimental.normalizeSafe(steer);

                headings[index] = new Heading
                {
                    value = math_experimental.normalizeSafe(forward + steer * 2.0f * bias[index&1023] * dt * Mathf.Deg2Rad * settings.rotationalSpeed )
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var maingroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(Boid)),
                ComponentType.ReadOnly(typeof(Position)),
                ComponentType.ReadOnly(typeof(BoidNearestObstaclePosition)),
                ComponentType.ReadOnly(typeof(BoidNearestTargetPosition)),
                typeof(Heading));

            var uniqueTypes = new List<Boid>(10);
            EntityManager.GetAllUniqueSharedComponentDatas(uniqueTypes);

            for (int typeIndex = 0; typeIndex < uniqueTypes.Count; typeIndex++)
            {
                var settings = uniqueTypes[typeIndex];
                var group = maingroup.GetVariation(settings);
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

                var hashBoidLocationsJob = new HashBoidLocations
                {
                    positions = positions,
                    cells = cells,
                    cellRadius = settings.cellRadius
                };

                var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(positions.Length, 64, inputDeps);

                var copyPositionsResults = new NativeArray<float3>(positions.Length, Allocator.TempJob);
                var copyPositionsJob = new CopyPosition
                {
                    positions = positions,
                    results = copyPositionsResults
                };
                var copyPositionsJobHandle = copyPositionsJob.Schedule(positions.Length, 64, inputDeps);

                var copyHeadingsResults = new NativeArray<float3>(positions.Length, Allocator.TempJob);
                var copyHeadingsJob = new CopyHeading
                {
                    headings = headings,
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
                    cellOffsetsTable = m_CellOffsetsTable,
                    bias = m_Bias,
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
                    bias = m_Bias,
                    dt = Time.deltaTime
                };

                inputDeps = steerJob.Schedule(positions.Length, 64, separationAndAlignmentSteerJobHandle);
                group.Dispose();
            }

			// The return value only applies to jobs working with injected components
            return inputDeps;
        }

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_Cells = new List<NativeMultiHashMap<int, int>>();
            m_CellOffsetsTable = new NativeArray<int3>(GridHash.cellOffsets, Allocator.Persistent);
            m_Bias = new NativeArray<float>(1024,Allocator.Persistent);
            for (int i = 0; i < 1024; i++)
            {
                m_Bias[i] = Random.Range(0.5f, 0.6f);
            }
        }

        protected override void OnDestroyManager()
        {
            for (int i = 0; i < m_Cells.Count; i++)
            {
                m_Cells[i].Dispose ();
            }
            m_CellOffsetsTable.Dispose();
            m_Bias.Dispose();
        }

    }
}

// Accumulate obstacles
// Accumulate targets
// Accumulate Headings with HeadingTarget tag
