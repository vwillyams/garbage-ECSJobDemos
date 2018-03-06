using System;
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
        private ComponentGroup m_TargetGroup;
        private ComponentGroup m_ObstacleGroup;
        private List<Boid> m_UniqueTypes = new List<Boid>(10);
        private List<PrevCells> m_PrevCells = new List<PrevCells>();

        struct PrevCells
        {
            public int boidCount;
            public NativeArray<float> neighborCount;
            public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            public NativeArray<int> cellIndices;
        }

        struct PositionHash : IEquatable<PositionHash>
        {
            public float3 Position;
            public int    Hash;

            public PositionHash(float3 position, int hash)
            {
                Position = position;
                Hash = hash;
            }

            public bool Equals(PositionHash other)
            {
                return Hash == other.Hash;
            }
        }

        [ComputeJobOptimization]
        struct HashPositions : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<PositionHash> positionHashes;
            public float cellRadius;

            public void Execute(int index)
            {
                var position = positions[index].Value;
                var hash = GridHash.Hash(position, cellRadius);
                positionHashes[index] = new PositionHash(position,hash);
            }
        }

        struct AlignmentSeparation
        {
            public float3 alignment;
            public float3 separation;
        }

        struct CellHashIndex
        {
            public int Hash;
            public int Value;
            public int Next;
        }

        [ComputeJobOptimization]
        struct Cells : IJob
        {
            [ReadOnly] public NativeArray<PositionHash> positionHashes;
            [ReadOnly] public NativeArray<float3> headings;
            public NativeArray<int> cellCount;
            public NativeArray<int> cellIndices;
            public NativeArray<float> neighborCount;
            public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            public NativeArray<CellHashIndex> hashCellBuckets;
            public int hashCellBucketCount;
            public int hashCellBucketBufferUsedCount;
            
            bool TryGetHashIndex(int hash, int hashBucketIndex, out int value)
            {
                var next = hashCellBuckets[hashBucketIndex];
                if (hash == 0)
                {
                    value = -1;
                    return false;
                }
                while (true)
                {
                    if (next.Hash.Equals(hash))
                    {
                        value = next.Value;
                        return true;
                    }
                    if (next.Next == 0)
                    {
                        value = -1;
                        return false;
                    }
                    next = hashCellBuckets[hashCellBucketCount + next.Next-1];
                }
            }
            
            void AddHashIdex(int hash, int hashBucketIndex, int value)
            {
                var first = hashCellBuckets[hashBucketIndex];
                if (first.Hash == 0)
                {
                    hashCellBuckets[hashBucketIndex] = new CellHashIndex
                    {
                        Hash = hash,
                        Value = value,
                        Next = 0 
                    };
                }
                else
                {
                    int nextBucketIndex = hashCellBucketBufferUsedCount;
                    hashCellBuckets[hashCellBucketCount + nextBucketIndex] = hashCellBuckets[hashBucketIndex];
                    hashCellBuckets[hashBucketIndex] = new CellHashIndex
                    {
                        Hash = hash,
                        Value = value,
                        Next = nextBucketIndex+1
                    };
                    hashCellBucketBufferUsedCount++;
                }
            }

            public void Execute()
            {
                var positionCount = positionHashes.Length;
                int nextCellIndex = 0;
                for (int i = 0; i < positionCount; i++)
                {
                    var hash = positionHashes[i].Hash;
                    var position = positionHashes[i].Position;
                    var hashBucketIndex = hash & (hashCellBucketCount - 1);
                    int cellIndex;
                    if (!TryGetHashIndex(hash, hashBucketIndex, out cellIndex))
                    {
                        AddHashIdex(hash, hashBucketIndex, nextCellIndex);
                        cellIndex = nextCellIndex;
                        cellAlignmentSeparation[cellIndex] = new AlignmentSeparation
                        {
                            separation = -position,
                            alignment = headings[i]
                        };
                        cellIndices[i] = cellIndex;
                        neighborCount[cellIndex] = 1.0f;
                        nextCellIndex++;
                    }
                    else
                    {
                        var separation = cellAlignmentSeparation[cellIndex].separation;
                        var alignment = cellAlignmentSeparation[cellIndex].alignment;
                        var otherSeparation = -position;
                        var otherAlignment = headings[i];

                        cellAlignmentSeparation[cellIndex] = new AlignmentSeparation
                        {
                            separation = separation + otherSeparation,
                            alignment = alignment + otherAlignment
                        };
                        cellIndices[i] = cellIndex;
                        neighborCount[cellIndex] += 1.0f;
                    }
                }

                cellCount[0] = nextCellIndex;
            }
        }

        struct TargetObstacle
        {
            public float3 heading;
            public bool1  avoidObstacle;
        }

        [ComputeJobOptimization]
        struct DivideAlignment : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> neighborCount;
            [ReadOnly] public NativeArray<int> cellCount;
            public NativeArray<AlignmentSeparation> cellAlignmentSeparation;

            public void Execute(int index)
            {
                if (index >= cellCount[0])
                {
                    return;
                }
                var alignment = cellAlignmentSeparation[index].alignment;
                var separation = cellAlignmentSeparation[index].separation;
                cellAlignmentSeparation[index] = new AlignmentSeparation
                {
                    alignment = alignment / neighborCount[index],
                    separation = separation
                };
            }
        }
        
        [ComputeJobOptimization]
        struct HeadingTargetObstacle : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public NativeArray<float3> targetPositions;
            [ReadOnly] public NativeArray<float3> obstaclePositions;
            [ReadOnly] public Boid settings;
            public NativeArray<TargetObstacle> targetObstacle;

            void NearestPosition(NativeArray<float3> targets, float3 position, out float3 nearestPosition, out float nearestDistance )
            {
                nearestPosition = targets[0];
                nearestDistance = math.lengthSquared(position-nearestPosition);
                for (int i = 1; i < targets.Length; i++)
                {
                    var targetPosition = targets[i];
                    var distance = math.lengthSquared(position-targetPosition);
                    var nearest = distance < nearestDistance;

                    nearestDistance = math.select(nearestDistance, distance, nearest);
                    nearestPosition = math.select(nearestPosition, targetPosition, nearest);
                }
                nearestDistance = math.sqrt(nearestDistance);
            }
            
            public void Execute(int index)
            {
                var position = positions[index].Value;
                
                float3 nearestObstaclePosition;
                float nearestObstacleDistance;
                NearestPosition(obstaclePositions,position,out nearestObstaclePosition,out nearestObstacleDistance);
                
                float3 nearestTargetPosition;
                float nearestTargetDistance;
                NearestPosition(targetPositions,position,out nearestTargetPosition,out nearestTargetDistance);
                
                var obstacleSteering = (position - nearestObstaclePosition);
                var avoidObstacleHeading = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                var targetHeading = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPosition - position);
                var nearestObstacleDistanceFromRadius = nearestObstacleDistance - settings.obstacleAversionDistance;
                if (nearestObstacleDistanceFromRadius < 0)
                {
                    targetObstacle[index] = new TargetObstacle
                    {
                        avoidObstacle = true,
                        heading = avoidObstacleHeading
                    };
                }
                else
                {
                    targetObstacle[index] = new TargetObstacle
                    {
                        avoidObstacle = false,
                        heading = targetHeading
                    };
                }
                    
            }
        }

        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int>                 cellIndices;
            [ReadOnly] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [ReadOnly] public NativeArray<float>               neighborCount;
            [ReadOnly] public Boid                             settings;
            [ReadOnly] public NativeArray<TargetObstacle>      targetObstacle;
            public float3                                      dt;
            public float3                                      alignmentWeight;
            public float3                                      separationWeight;
            public float3                                      speed;
            public ComponentDataArray<Position>                positions;
            public ComponentDataArray<Heading>                 headings;
            public ComponentDataArray<TransformMatrix>         transformMatrices;

            public void Execute(int index)
            {
                var forward              = headings[index].Value;
                var position             = positions[index].Value;
                var cellIndex            = cellIndices[index];
                var count                = neighborCount[cellIndex];
                var cellAlignment        = cellAlignmentSeparation[cellIndex].alignment;
                var cellSeparation       = cellAlignmentSeparation[cellIndex].separation;
                var targetHeading        = targetObstacle[index].heading;
                var avoidObstacle        = targetObstacle[index].avoidObstacle;
                
                var alignmentResult      = alignmentWeight * math_experimental.normalizeSafe(cellAlignment-forward);
                var separationResult     = separationWeight * math_experimental.normalizeSafe((position * count) + cellSeparation);
                var normalHeading        = math_experimental.normalizeSafe(alignmentResult + separationResult + targetHeading);
                var targetForward        = math.select(normalHeading, targetHeading, avoidObstacle);
                var nextHeading          = math_experimental.normalizeSafe(forward + dt*(targetForward-forward));
                var nextPosition         = position + (nextHeading * speed * dt);
                var rottrans             = math.lookRotationToMatrix(nextPosition, nextHeading, math.up());
                
                headings[index]          = new Heading {Value = nextHeading};
                positions[index]         = new Position {Value = nextPosition};
                transformMatrices[index] = new TransformMatrix { Value = rottrans };
            }
        }

        [ComputeJobOptimization]
        struct Dispose : IJob
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<PositionHash> positionHashes;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<TargetObstacle> targetObstacle;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> targetPositions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> obstaclePositions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<CellHashIndex> hashCellBuckets;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> copyHeadings;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellCount;
            
            public void Execute()
            {
            }
        }
        
        [ComputeJobOptimization]
        struct DisposePrevCells : IJob
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellIndices;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float> neighborCount;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            
            public void Execute()
            {
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(m_UniqueTypes);
            
            var obstacleSourcePositions = m_ObstacleGroup.GetComponentDataArray<Position>();
            var targetSourcePositions = m_TargetGroup.GetComponentDataArray<Position>();
            
            // Ingore typeIndex 0, can't use the default for anything meaningful.
            for (int typeIndex = 1; typeIndex < m_UniqueTypes.Count; typeIndex++)
            {
                var settings = m_UniqueTypes[typeIndex];
                var variation = m_MainGroup.GetVariation(settings);
                var positions = variation.GetComponentDataArray<Position>();
                var headings = variation.GetComponentDataArray<Heading>();
                var transformMatrices = variation.GetComponentDataArray<TransformMatrix>();
                variation.Dispose();

                var cacheIndex = typeIndex - 1;
                var boidCount = positions.Length;
                var cellCount = new NativeArray<int>(1, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var positionHashes = new NativeArray<PositionHash>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var copyHeadings = new NativeArray<float3>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var targetPositions = new NativeArray<float3>(targetSourcePositions.Length, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var targetObstacle = new NativeArray<TargetObstacle>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var obstaclePositions = new NativeArray<float3>(obstacleSourcePositions.Length, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var hashCellBucketCount =  math.ceil_pow2(boidCount*2);
                var hashCellBuckets = new NativeArray<CellHashIndex>(hashCellBucketCount+boidCount, Allocator.TempJob);
                
                var neighborCount = new NativeArray<float>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellAlignmentSeparation = new NativeArray<AlignmentSeparation>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

                PrevCells? prevCells = null;
                var nextCells = new PrevCells
                {
                    boidCount = boidCount,
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellIndices = cellIndices
                };
                
                if (cacheIndex > (m_PrevCells.Count - 1))
                {
                    m_PrevCells.Add(nextCells);
                }
                else if (m_PrevCells[cacheIndex].boidCount != boidCount)
                {
                    m_PrevCells[cacheIndex].cellAlignmentSeparation.Dispose();
                    m_PrevCells[cacheIndex].neighborCount.Dispose();
                    m_PrevCells[cacheIndex].cellIndices.Dispose();
                }
                else
                {
                    prevCells = m_PrevCells[cacheIndex];
                }
                m_PrevCells[cacheIndex] = nextCells;

                var hashPositionsJob = new HashPositions
                {
                    positions = positions,
                    positionHashes = positionHashes,
                    cellRadius = settings.cellRadius
                };
                var hashPositionsJobHandle = hashPositionsJob.Schedule(boidCount, 64, inputDeps);

                var copyHeadingsJob = new CopyComponentData<Heading, float3>
                {
                    source = headings,
                    results = copyHeadings
                };
                var copyHeadingsJobHandle = copyHeadingsJob.Schedule(boidCount, 64, inputDeps);

                var cellsBarrierJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, copyHeadingsJobHandle);
                
                var cellsJob = new Cells
                {
                    positionHashes = positionHashes,
                    cellIndices = cellIndices,
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    headings = copyHeadings,
                    hashCellBuckets = hashCellBuckets,
                    hashCellBucketBufferUsedCount = 0,
                    hashCellBucketCount = hashCellBucketCount,
                    cellCount = cellCount
                };
                var cellsJobHandle = cellsJob.Schedule(cellsBarrierJobHandle);

                var divideAlignmentJob = new DivideAlignment
                {
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    neighborCount = neighborCount,
                    cellCount = cellCount
                };
                var divideAlignmentJobHandle = divideAlignmentJob.Schedule(boidCount, 64, cellsJobHandle);

                var disposeBarrierJobHandle = divideAlignmentJobHandle;

                if (prevCells != null)
                {
                    var targetPositionsJob = new CopyComponentData<Position, float3>
                    {
                        source = targetSourcePositions,
                        results = targetPositions
                    };
                    var targetPositionsJobHandle = targetPositionsJob.Schedule(targetSourcePositions.Length,4,cellsBarrierJobHandle);
                
                    var obstaclePositionsJob = new CopyComponentData<Position, float3>
                    {
                        source = obstacleSourcePositions,
                        results = obstaclePositions
                    };
                    var obstaclePositionsJobHandle = obstaclePositionsJob.Schedule(obstacleSourcePositions.Length,4,cellsBarrierJobHandle);

                    var targetObstacleBarrierJobHandle = JobHandle.CombineDependencies(targetPositionsJobHandle, obstaclePositionsJobHandle);
                
                    var targetObstacleJob = new HeadingTargetObstacle
                    {
                        targetObstacle = targetObstacle,
                        targetPositions = targetPositions,
                        obstaclePositions = obstaclePositions,
                        positions = positions,
                        settings = settings,
                    };
                    var targetObstacleJobHandle = targetObstacleJob.Schedule(boidCount, 64, targetObstacleBarrierJobHandle);

                    var steerJob = new Steer
                    {
                        cellIndices = prevCells.Value.cellIndices,
                        cellAlignmentSeparation = prevCells.Value.cellAlignmentSeparation,
                        neighborCount = prevCells.Value.neighborCount,
                        targetObstacle = targetObstacle,
                        settings = settings,
                        dt = new float3(Time.deltaTime,Time.deltaTime,Time.deltaTime),
                        alignmentWeight = new float3(settings.alignmentWeight,settings.alignmentWeight,settings.alignmentWeight),
                        separationWeight = new float3(settings.separationWeight,settings.separationWeight,settings.separationWeight),
                        speed = new float3(settings.speed,settings.speed,settings.speed),
                        positions = positions,
                        headings = headings,
                        transformMatrices = transformMatrices
                    };
                    var steerJobHandle = steerJob.Schedule(boidCount, 64, targetObstacleJobHandle);
                    
                    var disposePrevCellsJob = new DisposePrevCells
                    {
                        cellAlignmentSeparation = prevCells.Value.cellAlignmentSeparation,
                        cellIndices = prevCells.Value.cellIndices,
                        neighborCount = prevCells.Value.neighborCount,
                    };
                    var disposePrevCellsJobHandle = disposePrevCellsJob.Schedule(steerJobHandle);
                    disposeBarrierJobHandle = JobHandle.CombineDependencies(disposePrevCellsJobHandle,disposeBarrierJobHandle);
                }

                var disposeJob = new Dispose
                {
                    targetObstacle = targetObstacle,
                    positionHashes = positionHashes,
                    targetPositions = targetPositions,
                    obstaclePositions = obstaclePositions,
                    hashCellBuckets = hashCellBuckets,
                    copyHeadings = copyHeadings,
                    cellCount = cellCount
                };
                var disposeJobHandle = disposeJob.Schedule(disposeBarrierJobHandle);

                inputDeps = disposeJobHandle;
            }
            m_UniqueTypes.Clear();

            return inputDeps;
        }

        protected override void OnCreateManager(int capacity)
        {
            m_MainGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(Boid)),
                typeof(Position),
                typeof(TransformMatrix),
                typeof(Heading));
            m_TargetGroup = GetComponentGroup(    
                ComponentType.ReadOnly(typeof(BoidTarget)),
                ComponentType.ReadOnly(typeof(Position)));
            m_ObstacleGroup = GetComponentGroup(    
                ComponentType.ReadOnly(typeof(BoidObstacle)),
                ComponentType.ReadOnly(typeof(Position)));
        }

        protected override void OnDestroyManager()
        {
            for (int i = 0; i < m_PrevCells.Count; i++)
            {
                m_PrevCells[i].cellAlignmentSeparation.Dispose();
                m_PrevCells[i].neighborCount.Dispose();
                m_PrevCells[i].cellIndices.Dispose();
            }
        }
    }
}
