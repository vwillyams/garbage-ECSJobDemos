using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleBounds;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    public class BoidSystem : JobComponentSystem
    {
        NativeMultiHashMap<int, int> 		 m_Cells;
        NativeArray<int3> 					 m_CellOffsetsTable;
        NativeArray<float>                   m_Bias;

        struct BoidSettingsGroup
        {
            [ReadOnly] public ComponentDataArray<BoidSettings> settings;
            public int Length;
        }

        [Inject] private BoidSettingsGroup m_BoidSettingsGroup;

        struct BoidGroup
        {
            [ReadOnly] public ComponentDataArray<Boid>       boid;
            [ReadOnly] public ComponentDataArray<Position>   positions;
            [ReadOnly] public ComponentDataArray<BoidNearestTargetPosition> nearestTargetPositions;
            [ReadOnly] public ComponentDataArray<BoidNearestObstaclePosition> nearestObstaclePositions;
            public ComponentDataArray<Heading>               headings;
            public int Length;
        }

        [Inject] private BoidGroup m_BoidGroup;

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
            [ReadOnly] public NativeArray<int3> 		   cellOffsetsTable;
            [ReadOnly] public NativeArray<float>           bias;
            [ReadOnly] public BoidSettings                 settings;
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
            [ReadOnly] public BoidSettings settings;
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
            if (m_BoidSettingsGroup.Length == 0)
            {
                return inputDeps;
            }

            if (m_BoidGroup.Length == 0)
            {
                return inputDeps;
            }

            // Only support one boid type until we can destroy m_Cells after jobs
            var settings = m_BoidSettingsGroup.settings[0];

            m_Cells.Capacity = math.max (m_Cells.Capacity, m_BoidGroup.Length);
            m_Cells.Clear();

            var hashBoidLocationsJob = new HashBoidLocations
            {
                positions = m_BoidGroup.positions,
                cells = m_Cells,
                cellRadius = settings.cellRadius
            };

            var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(m_BoidGroup.Length, 64, inputDeps);

            var copyPositionsResults = new NativeArray<float3>(m_BoidGroup.Length, Allocator.TempJob);
            var copyPositionsJob = new CopyPosition
            {
                positions = m_BoidGroup.positions,
                results = copyPositionsResults
            };
            var copyPositionsJobHandle = copyPositionsJob.Schedule(m_BoidGroup.Length,64,inputDeps);

            var copyHeadingsResults = new NativeArray<float3>(m_BoidGroup.Length, Allocator.TempJob);
            var copyHeadingsJob = new CopyHeading
            {
                headings = m_BoidGroup.headings,
                results = copyHeadingsResults
            };
            var copyHeadingsJobHandle = copyHeadingsJob.Schedule(m_BoidGroup.Length,64,inputDeps);

            var separationResults = new NativeArray<float3>(m_BoidGroup.Length, Allocator.TempJob);
            var alignmentResults = new NativeArray<float3>(m_BoidGroup.Length, Allocator.TempJob);
            var separationAndAlignmentSteerJob = new SeparationAndAlignmentSteer
            {
                positions = copyPositionsResults,
                headings = copyHeadingsResults,
                cells = m_Cells,
                settings = settings,
                cellOffsetsTable = m_CellOffsetsTable,
                bias = m_Bias,
                separationResults = separationResults,
                alignmentResults = alignmentResults,
            };
            var separationAndAlignmentSteerJobHandle = separationAndAlignmentSteerJob.Schedule(m_BoidGroup.Length,64,JobHandle.CombineDependencies(hashBoidLocationsJobHandle,copyHeadingsJobHandle,copyPositionsJobHandle));

            var steerJob = new Steer
            {
                positions = copyPositionsResults,
                headings = m_BoidGroup.headings,
                nearestTargetPositions = m_BoidGroup.nearestTargetPositions,
                nearestObstaclePositions = m_BoidGroup.nearestObstaclePositions,
                settings = settings,
                alignmentSteering = alignmentResults,
                separationSteering = separationResults,
                bias = m_Bias,
                dt = Time.deltaTime
            };

            var steerJobHandle = steerJob.Schedule(m_BoidGroup.Length,64,separationAndAlignmentSteerJobHandle);

            return steerJobHandle;
        }

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_Cells = new NativeMultiHashMap<int, int>(capacity,Allocator.Persistent);
            m_CellOffsetsTable = new NativeArray<int3>(GridHash.cellOffsets, Allocator.Persistent);
            m_Bias = new NativeArray<float>(1024,Allocator.Persistent);
            for (int i = 0; i < 1024; i++)
            {
                m_Bias[i] = Random.Range(0.5f, 0.6f);
            }
        }

        protected override void OnDestroyManager()
        {
            base.OnDestroyManager();
            m_Cells.Dispose ();
            m_CellOffsetsTable.Dispose();
            m_Bias.Dispose();
        }

    }
}

// Accumulate obstacles
// Accumulate targets
// Accumulate Headings with HeadingTarget tag
