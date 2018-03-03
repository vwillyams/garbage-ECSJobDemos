using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleRotation;
using Unity.Transforms;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    public class BoidSystem : JobComponentSystem
    {
        private ComponentGroup m_MainGroup;
        private ComponentGroup m_TargetGroup;
        private ComponentGroup m_ObstacleGroup;
        private List<Boid> m_UniqueTypes = new List<Boid>(10);

        struct CellHash : IEquatable<CellHash>
        {
            private int hash;

            public int Value => hash;

            public CellHash(int value)
            {
                hash = value;
            }

            public bool Equals(CellHash other)
            {
                return hash == other.hash;
            }

            public bool IsNull()
            {
                return hash == 0;
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

        struct AlignmentSeparation
        {
            public float3 alignment;
            public float3 separation;
        }

        struct CellHashIndex
        {
            public CellHash hash;
            public int value;
            public int next;
        }

        [ComputeJobOptimization]
        struct Cells : IJob
        {
            [ReadOnly] public NativeArray<CellHash> positionHashes;
            [ReadOnly] public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeArray<int> cellIndices;
            public NativeArray<int> neighborCount;
            public NativeArray<int> cellCount;
            public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            public NativeArray<CellHashIndex> hashCellBuckets;
            public int hashCellBucketCount;
            public int hashCellBucketBufferUsedCount;
            
            bool TryGetHashIndex(CellHash hash, int hashBucketIndex, out int value)
            {
                var next = hashCellBuckets[hashBucketIndex];
                if (next.hash.IsNull())
                {
                    value = -1;
                    return false;
                }
                while (true)
                {
                    if (next.hash.Equals(hash))
                    {
                        value = next.value;
                        return true;
                    }
                    if (next.next == 0)
                    {
                        value = -1;
                        return false;
                    }
                    next = hashCellBuckets[hashCellBucketCount + next.next-1];
                }
            }
            
            void AddHashIdex(CellHash hash, int hashBucketIndex, int value)
            {
                var first = hashCellBuckets[hashBucketIndex];
                if (first.hash.IsNull())
                {
                    hashCellBuckets[hashBucketIndex] = new CellHashIndex
                    {
                        hash = hash,
                        value = value,
                        next = 0 
                    };
                }
                else
                {
                    int nextBucketIndex = hashCellBucketBufferUsedCount;
                    hashCellBuckets[hashCellBucketCount + nextBucketIndex] = hashCellBuckets[hashBucketIndex];
                    hashCellBuckets[hashBucketIndex] = new CellHashIndex
                    {
                        hash = hash,
                        value = value,
                        next = nextBucketIndex+1
                    };
                    hashCellBucketBufferUsedCount++;
                }
            }

            public void Execute()
            {
                var positionCount = positions.Length;
                int nextCellIndex = 0;
                for (int i = 0; i < positionCount; i++)
                {
                    var hash = positionHashes[i];
                    var hashBucketIndex = hash.Value & (hashCellBucketCount - 1);
                    int cellIndex;
                    if (!TryGetHashIndex(hash, hashBucketIndex, out cellIndex))
                    {
                        AddHashIdex(hash, hashBucketIndex, nextCellIndex);
                        cellIndex = nextCellIndex;
                        cellAlignmentSeparation[cellIndex] = new AlignmentSeparation
                        {
                            separation = -positions[i].Value,
                            alignment = headings[i].Value
                        };
                        cellIndices[i] = cellIndex;
                        neighborCount[cellIndex] = 1;
                        nextCellIndex++;
                    }
                    else
                    {
                        var separation = cellAlignmentSeparation[cellIndex].separation;
                        var alignment = cellAlignmentSeparation[cellIndex].alignment;
                        var otherSeparation = -positions[i].Value;
                        var otherAlignment  = headings[i].Value;

                        cellAlignmentSeparation[cellIndex] = new AlignmentSeparation
                        {
                            separation = separation + otherSeparation,
                            alignment = alignment + otherAlignment
                        };
                        cellIndices[i] = cellIndex;
                        neighborCount[cellIndex]++;
                    }
                }

                cellCount[0] = nextCellIndex;
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

                targetObstacle[index] = new TargetObstacle
                {
                    avoidObstacle = nearestObstacleDistance - settings.obstacleAversionDistance,
                    obstacle = avoidObstacleHeading,
                    target = targetHeading
                };
            }
        }

        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> cellIndices;
            [ReadOnly] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [ReadOnly] public NativeArray<int> neighborCount;
            [ReadOnly] public Boid settings;
            [ReadOnly] public NativeArray<TargetObstacle> targetObstacle;
            public float dt;
            public ComponentDataArray<Position> positions;
            public ComponentDataArray<Heading> headings;

            public void Execute(int index)
            {
                var forward = headings[index].Value;
                var position = positions[index].Value;
                var cellIndex = cellIndices[index];
                var count = neighborCount[cellIndex];
                var alignmentSteering = cellAlignmentSeparation[cellIndex].alignment/count;
                var alignmentResult = settings.alignmentWeight * math_experimental.normalizeSafe(alignmentSteering-forward);
                var separationSteering = cellAlignmentSeparation[cellIndex].separation;
                var separationResult = settings.separationWeight * math_experimental.normalizeSafe((position * count) + separationSteering);
                var normalHeading = math_experimental.normalizeSafe(alignmentResult + separationResult + targetObstacle[index].target);
                var targetForward = math.select(normalHeading,targetObstacle[index].obstacle,targetObstacle[index].avoidObstacle < 0);
                var steer = math_experimental.normalizeSafe(forward + dt*(targetForward-forward));
                var speed = settings.speed;
                headings[index] = new Heading { Value = steer };
                positions[index] = new Position {Value = position + (steer * speed * dt)};
            }
        }

        [ComputeJobOptimization]
        struct Transform : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<Heading> headings;
            public ComponentDataArray<TransformMatrix> transformMatrices;

            public void Execute(int index)
            {
                float3 heading = math_experimental.normalizeSafe(headings[index].Value);
                float3 position = positions[index].Value;
                float4x4 rottrans = math.lookRotationToMatrix(position, heading, math.up());

                transformMatrices[index] = new TransformMatrix
                {
                    Value = rottrans
                };
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
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellCount;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> targetPositions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> obstaclePositions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<CellHashIndex> hashCellBuckets;
            
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

                var boidCount = positions.Length;
                var positionHashes = new NativeArray<CellHash>(boidCount, Allocator.TempJob);
                var neighborCount = new NativeArray<int>(boidCount, Allocator.TempJob);
                var cellAlignmentSeparation = new NativeArray<AlignmentSeparation>(boidCount, Allocator.TempJob);
                var cellCount = new NativeArray<int>(1, Allocator.TempJob);
                var targetPositions = new NativeArray<float3>(targetSourcePositions.Length, Allocator.TempJob);
                var targetObstacle = new NativeArray<TargetObstacle>(boidCount, Allocator.TempJob);
                var obstaclePositions = new NativeArray<float3>(obstacleSourcePositions.Length, Allocator.TempJob);
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob);
                var hashCellBucketCount =  math.ceil_pow2(boidCount*2);
                var hashCellBuckets = new NativeArray<CellHashIndex>(hashCellBucketCount+boidCount, Allocator.TempJob);

                var hashPositionsJob = new HashPositions
                {
                    positions = positions,
                    positionHashes = positionHashes,
                    cellRadius = settings.cellRadius
                };
                var hashPositionsJobHandle = hashPositionsJob.Schedule(boidCount, 1024, inputDeps);
                
                
                var targetPositionsJob = new CopyComponentData<Position, float3>
                {
                    source = targetSourcePositions,
                    results = targetPositions
                };
                var targetPositionsJobHandle = targetPositionsJob.Schedule(targetSourcePositions.Length,4,inputDeps);
                
                
                var obstaclePositionsJob = new CopyComponentData<Position, float3>
                {
                    source = obstacleSourcePositions,
                    results = obstaclePositions
                };
                var obstaclePositionsJobHandle = obstaclePositionsJob.Schedule(obstacleSourcePositions.Length,4,inputDeps);

                var cellsBarrierJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, targetPositionsJobHandle, obstaclePositionsJobHandle);
                
                var targetObstacleJob = new HeadingTargetObstacle
                {
                    targetObstacle = targetObstacle,
                    targetPositions = targetPositions,
                    obstaclePositions= obstaclePositions,
                    positions = positions,
                    settings = settings,
                };
                var targetObstacleJobHandle = targetObstacleJob.Schedule(boidCount, 1024, cellsBarrierJobHandle);
                
                var transformJob = new Transform
                {
                    positions = positions,
                    headings = headings,
                    transformMatrices = transformMatrices,
                };
                var transformJobHandle = transformJob.Schedule(boidCount, 1024, targetObstacleJobHandle);

                var cellsJob = new Cells
                {
                    positionHashes = positionHashes,
                    cellIndices = cellIndices,
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    positions = positions,
                    headings = headings,
                    cellCount = cellCount,
                    hashCellBuckets = hashCellBuckets,
                    hashCellBucketBufferUsedCount = 0,
                    hashCellBucketCount = hashCellBucketCount
                };
                var cellsJobHandle = cellsJob.Schedule(cellsBarrierJobHandle);
                
                var steerBarrierJobHandle = JobHandle.CombineDependencies(targetObstacleJobHandle, cellsJobHandle,transformJobHandle);
                
                var steerJob = new Steer
                {
                    positions = positions,
                    cellIndices = cellIndices,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    neighborCount = neighborCount,
                    targetObstacle = targetObstacle,
                    settings = settings,
                    dt = Time.deltaTime,
                    headings = headings
                };
                var steerJobHandle = steerJob.Schedule(boidCount, 1024, steerBarrierJobHandle);

                var disposeJob = new Dispose
                {
                    targetObstacle = targetObstacle,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellIndices = cellIndices,
                    neighborCount = neighborCount,
                    positionHashes = positionHashes,
                    cellCount = cellCount,
                    targetPositions = targetPositions,
                    obstaclePositions = obstaclePositions,
                    hashCellBuckets = hashCellBuckets
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
    }
}
