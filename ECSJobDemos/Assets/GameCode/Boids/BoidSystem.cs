using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using Unity.Transforms;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    [UpdateBefore(typeof(TransformSystem))]
    public class BoidSystem : JobComponentSystem
    {
        private ComponentGroup m_BoidGroup;
        private ComponentGroup m_TargetGroup;
        private ComponentGroup m_ObstacleGroup;
        private List<Boid> m_UniqueTypes = new List<Boid>(10);
        private List<PrevCells> m_PrevCells = new List<PrevCells>();

        struct PrevCells
        {
            public int boidCount;
            public NativeMultiHashMap<int, int> hashMap;
            public NativeArray<float> neighborCount;
            public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            public NativeArray<int> cellIndices;
        }

        struct AlignmentSeparation
        {
            public float3 alignment;
            public float3 separation;
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
            [ReadOnly] public ComponentDataArray<Heading>  headings;
            public NativeMultiHashMap<int, int>.Concurrent hashMap;
            public NativeArray<float>                      neighborCount;
            public NativeArray<AlignmentSeparation>        cellAlignmentSeparation;
            public float                                   cellRadius;

            public void Execute(int index)
            {
                var position = positions[index].Value;
                var hash = GridHash.Hash(position, cellRadius);
                hashMap.Add(hash, index);
                neighborCount[index] = 1;
                cellAlignmentSeparation[index] = new AlignmentSeparation
                {
                    alignment = headings[index].Value,
                    separation = -position
                };
            }
        }

        [ComputeJobOptimization]
        struct Cells : IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            // Anything with the same firstIndex is gauranteed to be running on the same thread, 
            // so there is not ParallelFor concurrency issue.
            [NativeDisableParallelForRestriction] public NativeArray<float> neighborCount;
            [NativeDisableParallelForRestriction] public NativeArray<AlignmentSeparation> cellAlignmentSeparation;
            [NativeDisableParallelForRestriction] public NativeArray<int> cellIndices;

            public void Execute(int firstIndex, int index)
            {
                cellIndices[index] = firstIndex;
                if (firstIndex != index)
                {
                    neighborCount[firstIndex] += 1.0f;
                    cellAlignmentSeparation[firstIndex] = new AlignmentSeparation
                    {
                        alignment = cellAlignmentSeparation[firstIndex].alignment + cellAlignmentSeparation[index].alignment,
                        separation = cellAlignmentSeparation[firstIndex].separation + cellAlignmentSeparation[index].separation
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
            [ReadOnly] public NativeArray<Position>            targetPositions;
            [ReadOnly] public NativeArray<Position>            obstaclePositions;
            [ReadOnly] public Boid                             settings;
            public float3                                      dt;
            public float3                                      alignmentWeight;
            public float3                                      separationWeight;
            public float3                                      speed;
            public ComponentDataArray<Position>                positions;
            public ComponentDataArray<Heading>                 headings;
            public ComponentDataArray<TransformMatrix>         transformMatrices;
            
            void NearestPosition(NativeArray<Position> targets, float3 position, out float3 nearestPosition, out float nearestDistance )
            {
                nearestPosition = targets[0].Value;
                nearestDistance = math.lengthSquared(position-nearestPosition);
                for (int i = 1; i < targets.Length; i++)
                {
                    var targetPosition = targets[i].Value;
                    var distance = math.lengthSquared(position-targetPosition);
                    var nearest = distance < nearestDistance;

                    nearestDistance = math.select(nearestDistance, distance, nearest);
                    nearestPosition = math.select(nearestPosition, targetPosition, nearest);
                }
                nearestDistance = math.sqrt(nearestDistance);
            }
            
            public void Execute(int index)
            {
                var forward              = headings[index].Value;
                var position             = positions[index].Value;
                var cellIndex            = cellIndices[index];
                var count                = neighborCount[cellIndex];
                var cellAlignment        = cellAlignmentSeparation[cellIndex].alignment;
                var cellSeparation       = cellAlignmentSeparation[cellIndex].separation;
                
                float3 nearestObstaclePosition;
                float nearestObstacleDistance;
                NearestPosition(obstaclePositions,position,out nearestObstaclePosition,out nearestObstacleDistance);
                
                float3 nearestTargetPosition;
                float nearestTargetDistance;
                NearestPosition(targetPositions,position,out nearestTargetPosition,out nearestTargetDistance);
                
                var obstacleSteering                  = (position - nearestObstaclePosition);
                var avoidObstacleHeading              = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                var targetHeading                     = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPosition - position);
                var nearestObstacleDistanceFromRadius = nearestObstacleDistance - settings.obstacleAversionDistance;
                
                var alignmentResult      = alignmentWeight * math_experimental.normalizeSafe((cellAlignment/count)-forward);
                var separationResult     = separationWeight * math_experimental.normalizeSafe((position * count) + cellSeparation);
                var normalHeading        = math_experimental.normalizeSafe(alignmentResult + separationResult + targetHeading);
                var targetForward        = math.select(normalHeading, avoidObstacleHeading, nearestObstacleDistanceFromRadius < 0);
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
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Position> targetPositions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Position> obstaclePositions;
            
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
                m_BoidGroup.SetFilter(settings);
                var positions = m_BoidGroup.GetComponentDataArray<Position>();
                var headings = m_BoidGroup.GetComponentDataArray<Heading>();
                var transformMatrices = m_BoidGroup.GetComponentDataArray<TransformMatrix>();

                var cacheIndex = typeIndex - 1;
                var boidCount = positions.Length;
                var targetPositions = new NativeArray<Position>(targetSourcePositions.Length, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var obstaclePositions = new NativeArray<Position>(obstacleSourcePositions.Length, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var neighborCount = new NativeArray<float>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellAlignmentSeparation = new NativeArray<AlignmentSeparation>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var hashMap = new NativeMultiHashMap<int,int>(boidCount,Allocator.TempJob);

                var nextCells = new PrevCells
                {
                    boidCount = boidCount,
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellIndices = cellIndices,
                    hashMap = hashMap
                };
                
                if (cacheIndex > (m_PrevCells.Count - 1))
                {
                    m_PrevCells.Add(nextCells);
                }
                else
                {
                    m_PrevCells[cacheIndex].cellAlignmentSeparation.Dispose();
                    m_PrevCells[cacheIndex].neighborCount.Dispose();
                    m_PrevCells[cacheIndex].cellIndices.Dispose();
                    m_PrevCells[cacheIndex].hashMap.Dispose();
                }
                m_PrevCells[cacheIndex] = nextCells;

                var hashPositionsJob = new HashPositions
                {
                    positions = positions,
                    headings = headings,
                    hashMap = hashMap,
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellRadius = settings.cellRadius
                };
                var hashPositionsJobHandle = hashPositionsJob.Schedule(boidCount, 64, inputDeps);

                var cellsJob = new Cells
                {
                    neighborCount = neighborCount,
                    cellAlignmentSeparation = cellAlignmentSeparation,
                    cellIndices = cellIndices,
                };
                var cellsJobHandle = cellsJob.Schedule(hashMap,64,hashPositionsJobHandle);

                var targetPositionsJob = new CopyComponentData<Position>
                {
                    source = targetSourcePositions,
                    results = targetPositions
                };
                var targetPositionsJobHandle = targetPositionsJob.Schedule(targetSourcePositions.Length,4,cellsJobHandle);
                
                var obstaclePositionsJob = new CopyComponentData<Position>
                {
                    source = obstacleSourcePositions,
                    results = obstaclePositions
                };
                var obstaclePositionsJobHandle = obstaclePositionsJob.Schedule(obstacleSourcePositions.Length,4,cellsJobHandle);

                var targetObstacleBarrierJobHandle = JobHandle.CombineDependencies(targetPositionsJobHandle, obstaclePositionsJobHandle);
                
                var steerJob = new Steer
                {
                    cellIndices = nextCells.cellIndices,
                    cellAlignmentSeparation = nextCells.cellAlignmentSeparation,
                    neighborCount = nextCells.neighborCount,
                    targetPositions = targetPositions,
                    obstaclePositions = obstaclePositions,
                    settings = settings,
                    dt = new float3(Time.deltaTime,Time.deltaTime,Time.deltaTime),
                    alignmentWeight = new float3(settings.alignmentWeight,settings.alignmentWeight,settings.alignmentWeight),
                    separationWeight = new float3(settings.separationWeight,settings.separationWeight,settings.separationWeight),
                    speed = new float3(settings.speed,settings.speed,settings.speed),
                    positions = positions,
                    headings = headings,
                    transformMatrices = transformMatrices
                };
                var steerJobHandle = steerJob.Schedule(boidCount, 64, targetObstacleBarrierJobHandle);
                    
                var disposeJob = new Dispose
                {
                    targetPositions = targetPositions,
                    obstaclePositions = obstaclePositions,
                };
                var disposeJobHandle = disposeJob.Schedule(steerJobHandle);

                inputDeps = disposeJobHandle;
            }
            m_UniqueTypes.Clear();
            
            return inputDeps;
        }

        protected override void OnCreateManager(int capacity)
        {
            m_BoidGroup = GetComponentGroup(
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
                m_PrevCells[i].hashMap.Dispose();
            }
        }
    }
}
