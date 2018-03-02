using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleRotation;
using Unity.Transforms;
using UnityEngine.ECS.Utilities;
using UnityEngine.Experimental.UIElements.StyleEnums;

namespace UnityEngine.ECS.Boids
{
    public class BoidSystem : JobComponentSystem
    {
        private ComponentGroup m_MainGroup;
        private List<Boid> m_UniqueTypes = new List<Boid>(10);
        private List<NativeHashMap<int,int>> m_Cells = new List<NativeHashMap<int,int>>();

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
        struct ClearCellIndices : IJob
        {
            public NativeHashMap<int, int> hashCellIndices;

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
        struct TrackBoidCells : IJob
        {
            [ReadOnly] public NativeArray<int> positionHashes;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellIndices;
            [NativeDisableParallelForRestriction] public NativeArray<int> neighborCount;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellCount;
            [NativeDisableParallelForRestriction] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [ReadOnly] public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeHashMap<int, int> hashCellIndices;

            public void Execute()
            {
                var positionCount = positions.Length;
                
                hashCellIndices.Clear();
                
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
                    cellIndices[i] = (int)cellIndex;
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
        struct DivideAlignmentBoidCells : IJobParallelFor
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

        struct ObstacleDistance
        {
            public float3 obstacle;
            public float3 distance;
        }
        
        [ComputeJobOptimization]
        struct HeadingObstacle : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<BoidNearestObstaclePosition> nearestObstaclePositions;
            [ReadOnly] public Boid settings;
            public NativeArray<ObstacleDistance> obstacleDistance;
            
            public void Execute(int index)
            {
                var position = positions[index].Value;
                var nearestObstaclePosition = nearestObstaclePositions[index].Value;
                var obstacleSteering = (position - nearestObstaclePosition);
                var distance = math.length(obstacleSteering);
                var avoidObstacleHeading = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;

                obstacleDistance[index] = new ObstacleDistance
                {
                    obstacle = avoidObstacleHeading,
                    distance = distance
                };
            }
        }

        [ComputeJobOptimization]
        struct HeadingTarget : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<BoidNearestTargetPosition> nearestTargetPositions;
            [ReadOnly] public Boid settings;
            public NativeArray<float3> target;
            
            public void Execute(int index)
            {
                var position = positions[index].Value;
                var result = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPositions[index].Value - position);
                target[index] = result;
            }
        }
        
        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> target;
            [ReadOnly] public NativeArray<ObstacleDistance> obstacleDistance;
            [ReadOnly] public NativeArray<AlignmentSeparation> alignmentSeparation;
            [ReadOnly] public Boid settings;
            public float dt;
            public ComponentDataArray<Heading> headings;

            public void Execute(int index)
            {
                var forward = headings[index].Value;
                var avoidObstacle = (obstacleDistance[index].distance < settings.obstacleAversionDistance);
                var normalHeading = math_experimental.normalizeSafe(alignmentSeparation[index].alignment + alignmentSeparation[index].separation + target[index]);
                var targetForward = math.select(normalHeading,obstacleDistance[index].obstacle,avoidObstacle);
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
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> positionHashes;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> target;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ObstacleDistance> obstacleDistance;
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

                var positionHashes = new NativeArray<int>(boidCount, Allocator.TempJob);
                var hashBoidLocationsJob = new HashBoidLocations
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
                
                NativeHashMap<int,int> cells;
                if (cacheIndex > (m_Cells.Count - 1))
                {
                    cells = new NativeHashMap<int,int>(boidCount,Allocator.Persistent);
                    m_Cells.Add(cells);
                }
                else
                {
                    cells = m_Cells[cacheIndex];
                    cells.Capacity = math.max(cells.Capacity, boidCount);
                }

                var clearCellIndicesJob = new ClearCellIndices
                {
                    hashCellIndices = cells
                };
                var clearCellIndicesJobHandle = clearCellIndicesJob.Schedule(inputDeps);

                var trackBarrierJobHandle =
                    JobHandle.CombineDependencies(hashBoidLocationsJobHandle, clearCellIndicesJobHandle);

                var trackBoidCellsJob = new TrackBoidCells
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
                
                var target = new NativeArray<float3>(boidCount, Allocator.TempJob);
                var targetJob = new HeadingTarget
                {
                    positions = positions,
                    nearestTargetPositions = nearestTargetPositions,
                    settings = settings,
                    target = target
                };
                var targetJobHandle = targetJob.Schedule(boidCount, 64, trackBarrierJobHandle);
                
                var obstacleDistance = new NativeArray<ObstacleDistance>(boidCount, Allocator.TempJob);
                var obstacleJob = new HeadingObstacle
                {
                    positions = positions,
                    nearestObstaclePositions = nearestObstaclePositions,
                    settings = settings,
                    obstacleDistance = obstacleDistance
                };
                var obstacleJobHandle = obstacleJob.Schedule(boidCount, 64, trackBarrierJobHandle);
                
                var divideAlignmentBoidCellsJob = new DivideAlignmentBoidCells
                {
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellCount = cellCount
                };
                var divideAlignmentBoidCellsJobHandle = divideAlignmentBoidCellsJob.Schedule(boidCount, 1024, trackBoidCellsJobHandle);

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
                var alignmentSeparationJobHandle = alignmentSeparationJob.Schedule(boidCount, 1024, divideAlignmentBoidCellsJobHandle);

                var weightedHeadingsJobHandle =
                    JobHandle.CombineDependencies(targetJobHandle, obstacleJobHandle, alignmentSeparationJobHandle);
                
                var steerJob = new Steer
                {
                    target = target,
                    obstacleDistance = obstacleDistance,
                    alignmentSeparation = alignmentSeparation,
                    settings = settings,
                    dt = Time.deltaTime,
                    headings = headings
                };
                var steerJobHandle = steerJob.Schedule(boidCount, 1024, weightedHeadingsJobHandle);

                var disposeJob = new Dispose
                {
                    target = target,
                    obstacleDistance = obstacleDistance,
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
