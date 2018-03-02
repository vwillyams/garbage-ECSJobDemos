using System;
using System.Collections.Generic;
using Microsoft.Msagl.Core.DataStructures;
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
        private ComponentGroup m_MainGroup;
        private List<Boid> m_UniqueTypes = new List<Boid>(10);
        private List<NativeHashMap<CellHash,int>> m_Cells = new List<NativeHashMap<CellHash,int>>();

        struct CellHash : IEquatable<CellHash>
        {
            private int hash;

            public int Value
            {
                get { return hash; }
                set { hash = value; }
            }

            public CellHash(int value)
            {
                hash = value;
            }

            public override int GetHashCode()
            {
                return hash;
            }

            public bool Equals(CellHash other)
            {
                return hash == other.hash;
            }
        }

        [ComputeJobOptimization]
        struct HashPositions : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<CellHash> positionHashes;
            public float cellRadius;

            public void Execute(int index)
            {
                var hash = GridHash.Hash(positions[index].Value, cellRadius);
                positionHashes[index] = new CellHash(hash);
            }
        }

        [ComputeJobOptimization]
        struct ClearHashCellIndices : IJob
        {
            public NativeHashMap<CellHash, int> hashCellIndices;

            public void Execute()
            {
                hashCellIndices.Clear();
            }
        }

        struct AlignmentSeparation
        {
            public float3 alignment;
            public float3 separation;
        }

        [ComputeJobOptimization]
        struct Cells : IJob
        {
            [ReadOnly] public NativeArray<CellHash> positionHashes;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellIndices;
            [NativeDisableParallelForRestriction] public NativeArray<int> neighborCount;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellCount;
            [NativeDisableParallelForRestriction] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [ReadOnly] public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeHashMap<CellHash, int> hashCellIndices;

            public void Execute()
            {
                var positionCount = positions.Length;
                int nextCellIndex = 0;
                for (int i = 0; i < positionCount; i++)
                {
                    var hash = positionHashes[i];
                    int cellIndex;
                    if (!hashCellIndices.TryGetValue(hash, out cellIndex))
                    {
                        hashCellIndices.TryAdd(hash, nextCellIndex);
                        cellIndex = nextCellIndex;
                        neighborCount[cellIndex] = 0;
                        cellAlignmentSeparation[cellIndex] = new AlignmentSeparation();
                        nextCellIndex++;
                    }
                    cellIndices[i] = cellIndex;
                    neighborCount[cellIndex]++;

                    var separation = cellAlignmentSeparation[cellIndex].separation;
                    var alignment = cellAlignmentSeparation[cellIndex].alignment;
                    var otherSeparation = -positions[i].Value;
                    var otherAlignment  = headings[i].Value;

                    cellAlignmentSeparation[cellIndex] = new AlignmentSeparation
                    {
                        separation = separation + otherSeparation,
                        alignment = alignment + otherAlignment
                    };
                }

                cellCount[0] = nextCellIndex;
            }
        }

        [ComputeJobOptimization]
        struct CellsDivideAlignment : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> cellCount;
            public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            public NativeArray<int> neighborCount;

            public void Execute(int cellIndex)
            {
                if (cellIndex > cellCount[0])
                {
                    return;
                }
                
                var alignment = cellAlignmentSeparation[cellIndex].alignment;
                var separation = cellAlignmentSeparation[cellIndex].separation;
                var boidCount = neighborCount[cellIndex];

                cellAlignmentSeparation[cellIndex] = new AlignmentSeparation
                {
                    alignment = alignment / boidCount,
                    separation = separation
                };
            }
        }

        [ComputeJobOptimization]
        struct HeadingAlignmentSeparation : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public NativeArray<int> cellIndices;
            [ReadOnly] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [ReadOnly] public NativeArray<int> neighborCount;
            [ReadOnly] public Boid settings;
            public NativeArray<AlignmentSeparation> alignmentSeparation;
            
            public void Execute(int index)
            {
                var forward = headings[index].Value;
                var position = positions[index].Value;
                var cellIndex = cellIndices[index];
                var alignmentSteering = cellAlignmentSeparation[cellIndex].alignment;
                var alignmentResult = settings.alignmentWeight * math_experimental.normalizeSafe(alignmentSteering-forward);
                var count = neighborCount[cellIndex];
                var separationSteering = cellAlignmentSeparation[cellIndex].separation;
                var separationResult = settings.separationWeight * math_experimental.normalizeSafe((position * count) + separationSteering);
                
                alignmentSeparation[index] = new AlignmentSeparation
                {
                    separation = separationResult,
                    alignment = alignmentResult
                };
            }
        }

        struct TargetObstacle
        {
            public float3 target;
            public float3 obstacle;
            public float avoidObstacle;
        }
        
        [ComputeJobOptimization]
        struct HeadingTargetObstacle : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<BoidNearestObstaclePosition> nearestObstaclePositions;
            [ReadOnly] public ComponentDataArray<BoidNearestTargetPosition> nearestTargetPositions;
            [ReadOnly] public Boid settings;
            public NativeArray<TargetObstacle> targetObstacle;
            
            public void Execute(int index)
            {
                var position = positions[index].Value;
                var nearestObstaclePosition = nearestObstaclePositions[index].Value;
                var obstacleSteering = (position - nearestObstaclePosition);
                var obstacleDistance = math.length(obstacleSteering);
                var avoidObstacleHeading = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                var targetHeading = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPositions[index].Value - position);

                targetObstacle[index] = new TargetObstacle
                {
                    avoidObstacle = obstacleDistance - settings.obstacleAversionDistance,
                    obstacle = avoidObstacleHeading,
                    target = targetHeading
                };
            }
        }

        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TargetObstacle> targetObstacle;
            [ReadOnly] public NativeArray<AlignmentSeparation> alignmentSeparation;
            [ReadOnly] public Boid settings;
            public float dt;
            public ComponentDataArray<Heading> headings;

            public void Execute(int index)
            {
                var forward = headings[index].Value;
                var normalHeading = math_experimental.normalizeSafe(alignmentSeparation[index].alignment + alignmentSeparation[index].separation + targetObstacle[index].target);
                var targetForward = math.select(normalHeading,targetObstacle[index].obstacle,targetObstacle[index].avoidObstacle < 0);
                var steer = math_experimental.normalizeSafe(forward + dt*(targetForward-forward));
                headings[index] = new Heading { Value = steer };
            }
        }
        
        [ComputeJobOptimization]
        struct Dispose : IJob
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellIndices;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> neighborCount;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<CellHash> positionHashes;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<TargetObstacle> targetObstacle;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<AlignmentSeparation> alignmentSeparation;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellCount;

            public void Execute()
            {
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(m_UniqueTypes);

            // Ingore typeIndex 0, can't use the default for anything meaningful.
            for (int typeIndex = 1; typeIndex < m_UniqueTypes.Count; typeIndex++)
            {
                var settings = m_UniqueTypes[typeIndex];
                var cacheIndex = typeIndex - 1;
                var variation = m_MainGroup.GetVariation(settings);
                var positions = variation.GetComponentDataArray<Position>();
                var nearestObstaclePositions = variation.GetComponentDataArray<BoidNearestObstaclePosition>();
                var nearestTargetPositions = variation.GetComponentDataArray<BoidNearestTargetPosition>();
                var headings = variation.GetComponentDataArray<Heading>();
                variation.Dispose();

                var boidCount = positions.Length;

                var positionHashes = new NativeArray<CellHash>(boidCount, Allocator.TempJob);
                var hashBoidLocationsJob = new HashPositions
                {
                    positions = positions,
                    positionHashes = positionHashes,
                    cellRadius = settings.cellRadius
                };
                var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(boidCount, 64, inputDeps);
                
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob);
                var neighborCount = new NativeArray<int>(boidCount, Allocator.TempJob);
                var cellAlignmentSeparation = new NativeArray<AlignmentSeparation>(boidCount, Allocator.TempJob);
                var cellCount = new NativeArray<int>(1, Allocator.TempJob);
                
                NativeHashMap<CellHash,int> cells;
                if (cacheIndex > (m_Cells.Count - 1))
                {
                    cells = new NativeHashMap<CellHash,int>(boidCount,Allocator.Persistent);
                    m_Cells.Add(cells);
                }
                else
                {
                    cells = m_Cells[cacheIndex];
                    cells.Capacity = math.max(cells.Capacity, boidCount);
                }

                var clearCellIndicesJob = new ClearHashCellIndices
                {
                    hashCellIndices = cells
                };
                var clearCellIndicesJobHandle = clearCellIndicesJob.Schedule(inputDeps);

                var trackBarrierJobHandle = JobHandle.CombineDependencies(hashBoidLocationsJobHandle, clearCellIndicesJobHandle);

                var trackBoidCellsJob = new Cells
                {
                    positionHashes = positionHashes,
                    cellIndices = cellIndices,
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    positions = positions,
                    headings = headings,
                    cellCount = cellCount,
                    hashCellIndices = cells
                };
                var trackBoidCellsJobHandle = trackBoidCellsJob.Schedule(trackBarrierJobHandle);
                
                var targetObstacle = new NativeArray<TargetObstacle>(boidCount, Allocator.TempJob);
                var targetObstacleJob = new HeadingTargetObstacle
                {
                    positions = positions,
                    nearestObstaclePositions = nearestObstaclePositions,
                    nearestTargetPositions = nearestTargetPositions,
                    settings = settings,
                    targetObstacle = targetObstacle
                };
                var targetObstacleJobHandle = targetObstacleJob.Schedule(boidCount, 64, trackBarrierJobHandle);
                
                var divideAlignmentBoidCellsJob = new CellsDivideAlignment
                {
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellCount = cellCount
                };
                var divideAlignmentBoidCellsJobHandle = divideAlignmentBoidCellsJob.Schedule(boidCount, 64, trackBoidCellsJobHandle);

                var alignmentSeparation = new NativeArray<AlignmentSeparation>(boidCount,Allocator.TempJob);
                var alignmentSeparationJob = new HeadingAlignmentSeparation
                {
                    headings = headings,
                    positions = positions,
                    cellIndices = cellIndices,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    neighborCount = neighborCount,
                    settings = settings,
                    alignmentSeparation = alignmentSeparation
                };
                var alignmentSeparationJobHandle = alignmentSeparationJob.Schedule(boidCount, 64, divideAlignmentBoidCellsJobHandle);

                var steerBarrierJobHandle = JobHandle.CombineDependencies(targetObstacleJobHandle, alignmentSeparationJobHandle);
                
                var steerJob = new Steer
                {
                    targetObstacle = targetObstacle,
                    alignmentSeparation = alignmentSeparation,
                    settings = settings,
                    dt = Time.deltaTime,
                    headings = headings
                };
                var steerJobHandle = steerJob.Schedule(boidCount, 64, steerBarrierJobHandle);

                var disposeJob = new Dispose
                {
                    targetObstacle = targetObstacle,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellIndices = cellIndices,
                    neighborCount = neighborCount,
                    alignmentSeparation = alignmentSeparation,
                    positionHashes = positionHashes,
                    cellCount = cellCount
                };
                var disposeJobHandle = disposeJob.Schedule(steerJobHandle);
                
                inputDeps = disposeJobHandle;
            }
            m_UniqueTypes.Clear();

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

        protected override void OnDestroyManager()
        {
            for (int i = 0; i < m_Cells.Count; i++)
            {
                m_Cells[i].Dispose();
            }
        }
    }
}
