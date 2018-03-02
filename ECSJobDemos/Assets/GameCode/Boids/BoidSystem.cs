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

        struct CellSteering
        {
            public float3 alignment;
            public float3 separation;
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

        [ComputeJobOptimization]
        struct TrackBoidCells : IJob
        {
            [ReadOnly] public NativeArray<int> positionHashes;
            [NativeDisableParallelForRestriction] public NativeArray<int> boidCellIndices;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellBoidCount;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellCount;
            [NativeDisableParallelForRestriction] public NativeArray<CellSteering> cellSteerings;
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
                        cellBoidCount[cellIndex] = 0;
                        cellSteerings[cellIndex] = default(CellSteering);
                        nextCellIndex++;
                    }
                    boidCellIndices[i] = cellIndex;
                    cellBoidCount[cellIndex]++;

                    var separation = cellSteerings[cellIndex].separation;
                    var alignment  = cellSteerings[cellIndex].alignment;
                    var otherSeparation = -positions[i].Value;
                    var otherAlignment  = headings[i].Value;
                    
                    cellSteerings[cellIndex] = new CellSteering
                    {
                        separation = separation + otherSeparation,
                        alignment  = alignment + otherAlignment
                    };
                }

                cellCount[0] = nextCellIndex;
            }
        }

        [ComputeJobOptimization]
        struct DivideAlignmentBoidCells : IJobParallelFor
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellCount;
            public NativeArray<CellSteering> cellSteerings;
            public NativeArray<int> cellBoidCount;

            public void Execute(int cellIndex)
            {
                if (cellIndex > cellCount[0])
                {
                    return;
                }
                var separation = cellSteerings[cellIndex].separation;
                var alignment  = cellSteerings[cellIndex].alignment;
                var boidCount = cellBoidCount[cellIndex];

                cellSteerings[cellIndex] = new CellSteering
                {
                    separation = separation,
                    alignment = alignment / boidCount
                };
            }
        }
        
        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<Position> positions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> sharedIndices;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> sharedValueIndexCounts;
            [ReadOnly] public ComponentDataArray<BoidNearestTargetPosition> nearestTargetPositions;
            [ReadOnly] public ComponentDataArray<BoidNearestObstaclePosition> nearestObstaclePositions;
            [ReadOnly] public Boid settings;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<CellSteering> cellSteerings;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> positionHashes;
            public float dt;

            public void Execute(int index)
            {
                var position = positions[index].Value;
                var forward = headings[index].Value;
                var nearestObstaclePosition = nearestObstaclePositions[index].Value;
                var obstacleSteering = (position - nearestObstaclePosition);
                var avoidObstacle = (math.length(obstacleSteering) < settings.obstacleAversionDistance);
                var sharedIndex = sharedIndices[index];
                var neighborCount = sharedValueIndexCounts[sharedIndex];
                var alignmentSteering = cellSteerings[sharedIndex].alignment;
                var separationSteering = cellSteerings[sharedIndex].separation;
                var s0 = settings.alignmentWeight * math_experimental.normalizeSafe(alignmentSteering-forward);
                var s1 = settings.separationWeight * math_experimental.normalizeSafe((position * neighborCount) + separationSteering);
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
                     
                var positionHashes = new NativeArray<int>(positions.Length, Allocator.TempJob);
                var hashBoidLocationsJob = new HashBoidLocations
                {
                    positions = positions,
                    positionHashes = positionHashes,
                    cellRadius = settings.cellRadius
                };
                var hashBoidLocationsJobHandle = hashBoidLocationsJob.Schedule(positions.Length, 64, inputDeps);
                
                var boidCellIndices = new NativeArray<int>(positions.Length, Allocator.TempJob);
                var cellBoidCount = new NativeArray<int>(positions.Length, Allocator.TempJob);
                var cellSteerings = new NativeArray<CellSteering>(positions.Length, Allocator.TempJob);
                var cellCount = new NativeArray<int>(1, Allocator.TempJob);
                
                NativeHashMap<int,int> cells;
                if (cacheIndex > (m_Cells.Count - 1))
                {
                    cells = new NativeHashMap<int,int>(positions.Length,Allocator.Persistent);
                    m_Cells.Add(cells);
                }
                else
                {
                    cells = m_Cells[cacheIndex];
                    cells.Capacity = math.max(cells.Capacity, positions.Length);
                }

                var clearCellIndicesJob = new ClearCellIndices
                {
                    hashCellIndices = cells
                };
                var clearCellIndicesJobHandle = clearCellIndicesJob.Schedule(inputDeps);

                inputDeps = JobHandle.CombineDependencies(hashBoidLocationsJobHandle, clearCellIndicesJobHandle);
                
                var trackBoidCellsJob = new TrackBoidCells
                {
                    positionHashes = positionHashes,
                    boidCellIndices = boidCellIndices,
                    cellBoidCount = cellBoidCount,
                    cellSteerings = cellSteerings,
                    positions = positions,
                    headings = headings,
                    cellCount = cellCount,
                    hashCellIndices = cells
                };
                inputDeps = trackBoidCellsJob.Schedule(inputDeps);
                    
                var divideAlignmentBoidCellsJob = new DivideAlignmentBoidCells
                {
                    cellBoidCount = cellBoidCount,
                    cellSteerings = cellSteerings,
                    cellCount = cellCount
                };
                inputDeps = divideAlignmentBoidCellsJob.Schedule(positions.Length, 1024, inputDeps);
                
                var steerJob = new Steer
                {
                    positions = positions,
                    headings = headings,
                    sharedIndices = boidCellIndices,
                    sharedValueIndexCounts = cellBoidCount,
                    nearestTargetPositions = nearestTargetPositions,
                    nearestObstaclePositions = nearestObstaclePositions,
                    settings = settings,
                    cellSteerings = cellSteerings,
                    positionHashes = positionHashes,
                    dt = Time.deltaTime
                };

                inputDeps = steerJob.Schedule(positions.Length, 1024, inputDeps);
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
