using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using Unity.Transforms;
using UnityEngine;
using Samples.Common;

namespace Samples.Boids
{
    [UpdateBefore(typeof(TransformSystem))]
    public class BoidSystem : JobComponentSystem
    {
        private ComponentGroup  m_BoidGroup;
        private ComponentGroup  m_TargetGroup;
        private ComponentGroup  m_ObstacleGroup;
        
        private List<Boid>      m_UniqueTypes = new List<Boid>(10);
        private List<PrevCells> m_PrevCells   = new List<PrevCells>();

        struct PrevCells
        {
            public NativeMultiHashMap<int, int> hashMap;
            public NativeArray<int>             cellIndices;
            public NativeArray<float3>          cellAlignment;
            public NativeArray<float3>          cellSeparation;
            public NativeArray<int>             cellObstaclePositionIndex;
            public NativeArray<float>           cellObstacleDistance;
            public NativeArray<int>             cellTargetPistionIndex;
            public NativeArray<int>             cellCount;
        }

        [ComputeJobOptimization]
        struct HashPositions : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<Heading>  headings;
            public NativeMultiHashMap<int, int>.Concurrent hashMap;
            public NativeArray<int>                        cellCount;
            public NativeArray<float3>                     cellAlignment;
            public NativeArray<float3>                     cellSeparation;
            public float                                   cellRadius;

            public void Execute(int index)
            {
                var position = positions[index].Value;
                var hash     = GridHash.Hash(position, cellRadius);
                
                hashMap.Add(hash, index);
                
                cellCount[index]      = 1;
                cellAlignment[index]  = headings[index].Value;
                cellSeparation[index] = -position;
            }
        }

        [ComputeJobOptimization]
        struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            public NativeArray<int>                        cellIndices;
            public NativeArray<float3>                     cellAlignment;
            public NativeArray<float3>                     cellSeparation;
            public NativeArray<int>                        cellObstaclePositionIndex;
            public NativeArray<float>                      cellObstacleDistance;
            public NativeArray<int>                        cellTargetPistionIndex;
            public NativeArray<int>                        cellCount;
            [ReadOnly] public ComponentDataArray<Position> targetPositions;
            [ReadOnly] public ComponentDataArray<Position> obstaclePositions;
            
            void NearestPosition(ComponentDataArray<Position> targets, float3 position, out int nearestPositionIndex, out float nearestDistance )
            {
                nearestPositionIndex = 0;
                nearestDistance      = math.lengthSquared(position-targets[0].Value);
                for (int i = 1; i < targets.Length; i++)
                {
                    var targetPosition = targets[i].Value;
                    var distance       = math.lengthSquared(position-targetPosition);
                    var nearest        = distance < nearestDistance;

                    nearestDistance      = math.select(nearestDistance, distance, nearest);
                    nearestPositionIndex = math.select(nearestPositionIndex, i, nearest);
                }
                nearestDistance = math.sqrt(nearestDistance);
            }
            
            public void Execute(int cellIndex, int index)
            {
                if (cellIndex == index)
                {
                    var position = cellSeparation[cellIndex] / -cellCount[cellIndex];

                    int   obstaclePositionIndex;
                    float obstacleDistance;
                    NearestPosition(obstaclePositions, position, out obstaclePositionIndex, out obstacleDistance);
                    cellObstaclePositionIndex[cellIndex] = obstaclePositionIndex;
                    cellObstacleDistance[cellIndex]      = obstacleDistance;

                    int   targetPositionIndex;
                    float targetDistance;
                    NearestPosition(targetPositions, position, out targetPositionIndex, out targetDistance);
                    cellTargetPistionIndex[cellIndex] = targetPositionIndex;
                }
                else
                {
                    cellCount[cellIndex]      += 1;
                    cellAlignment[cellIndex]  += cellAlignment[index];
                    cellSeparation[cellIndex] += cellSeparation[index];
                }
                
                cellIndices[index] = cellIndex;
            }
        }

        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int>             cellIndices;
            [ReadOnly] public Boid                         settings;
            [ReadOnly] public ComponentDataArray<Position> targetPositions;
            [ReadOnly] public ComponentDataArray<Position> obstaclePositions;
            [ReadOnly] public NativeArray<float3>          cellAlignment;
            [ReadOnly] public NativeArray<float3>          cellSeparation;
            [ReadOnly] public NativeArray<int>             cellObstaclePositionIndex;
            [ReadOnly] public NativeArray<float>           cellObstacleDistance;
            [ReadOnly] public NativeArray<int>             cellTargetPistionIndex;
            [ReadOnly] public NativeArray<int>             cellCount;
            public float                                   dt;
            public ComponentDataArray<Position>            positions;
            public ComponentDataArray<Heading>             headings;
            public ComponentDataArray<TransformMatrix>     transformMatrices;
            
            public void Execute(int index)
            {
                var forward                           = headings[index].Value;
                var position                          = positions[index].Value;
                var cellIndex                         = cellIndices[index];
                var neighborCount                     = cellCount[cellIndex];
                var alignment                         = cellAlignment[cellIndex];
                var separation                        = cellSeparation[cellIndex];
                var nearestObstacleDistance           = cellObstacleDistance[cellIndex];
                var nearestObstaclePositionIndex      = cellObstaclePositionIndex[cellIndex];
                var nearestTargetPositionIndex        = cellTargetPistionIndex[cellIndex];
                var nearestObstaclePosition           = obstaclePositions[nearestObstaclePositionIndex].Value;
                var nearestTargetPosition             = targetPositions[nearestTargetPositionIndex].Value;
                
                var obstacleSteering                  = position - nearestObstaclePosition;
                var avoidObstacleHeading              = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                var targetHeading                     = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPosition - position);
                var nearestObstacleDistanceFromRadius = nearestObstacleDistance - settings.obstacleAversionDistance;
                var alignmentResult                   = settings.alignmentWeight * math_experimental.normalizeSafe((alignment/neighborCount)-forward);
                var separationResult                  = settings.separationWeight * math_experimental.normalizeSafe((position * neighborCount) + separation);
                var normalHeading                     = math_experimental.normalizeSafe(alignmentResult + separationResult + targetHeading);
                var targetForward                     = math.select(normalHeading, avoidObstacleHeading, nearestObstacleDistanceFromRadius < 0);
                var nextHeading                       = math_experimental.normalizeSafe(forward + dt*(targetForward-forward));
                var nextPosition                      = position + (nextHeading * settings.speed * dt);
                var rottrans                          = math.lookRotationToMatrix(nextPosition, nextHeading, math.up());
                
                headings[index]                       = new Heading {Value = nextHeading};
                positions[index]                      = new Position {Value = nextPosition};
                transformMatrices[index]              = new TransformMatrix { Value = rottrans };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(m_UniqueTypes);
            
            var obstaclePositions = m_ObstacleGroup.GetComponentDataArray<Position>();
            var targetPositions   = m_TargetGroup.GetComponentDataArray<Position>();
            
            // Ingore typeIndex 0, can't use the default for anything meaningful.
            for (int typeIndex = 1; typeIndex < m_UniqueTypes.Count; typeIndex++)
            {
                var settings = m_UniqueTypes[typeIndex];
                m_BoidGroup.SetFilter(settings);
                
                var positions         = m_BoidGroup.GetComponentDataArray<Position>();
                var headings          = m_BoidGroup.GetComponentDataArray<Heading>();
                var transformMatrices = m_BoidGroup.GetComponentDataArray<TransformMatrix>();

                var cacheIndex                = typeIndex - 1;
                var boidCount                 = positions.Length;
                var cellIndices               = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var hashMap                   = new NativeMultiHashMap<int,int>(boidCount,Allocator.TempJob);
                var cellAlignment             = new NativeArray<float3>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellSeparation            = new NativeArray<float3>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellObstacleDistance      = new NativeArray<float>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellObstaclePositionIndex = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellTargetPositionIndex   = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellCount                 = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

                var nextCells = new PrevCells
                {
                    cellIndices               = cellIndices,
                    hashMap                   = hashMap,
                    cellAlignment             = cellAlignment,
                    cellSeparation            = cellSeparation,
                    cellObstacleDistance      = cellObstacleDistance,
                    cellObstaclePositionIndex = cellObstaclePositionIndex,
                    cellTargetPistionIndex    = cellTargetPositionIndex,
                    cellCount                 = cellCount
                };
                
                if (cacheIndex > (m_PrevCells.Count - 1))
                {
                    m_PrevCells.Add(nextCells);
                }
                else
                {
                    m_PrevCells[cacheIndex].cellIndices.Dispose();
                    m_PrevCells[cacheIndex].hashMap.Dispose();
                    m_PrevCells[cacheIndex].cellAlignment.Dispose();
                    m_PrevCells[cacheIndex].cellSeparation.Dispose();
                    m_PrevCells[cacheIndex].cellObstacleDistance.Dispose();
                    m_PrevCells[cacheIndex].cellObstaclePositionIndex.Dispose();
                    m_PrevCells[cacheIndex].cellTargetPistionIndex.Dispose();
                    m_PrevCells[cacheIndex].cellCount.Dispose();
                }
                m_PrevCells[cacheIndex] = nextCells;

                var hashPositionsJob = new HashPositions
                {
                    positions      = positions,
                    headings       = headings,
                    hashMap        = hashMap,
                    cellCount      = cellCount,
                    cellAlignment  = cellAlignment,
                    cellSeparation = cellSeparation,
                    cellRadius     = settings.cellRadius
                };
                var hashPositionsJobHandle = hashPositionsJob.Schedule(boidCount, 64, inputDeps);

                var mergeCellsJob = new MergeCells
                {
                    cellIndices               = cellIndices,
                    cellAlignment             = cellAlignment,
                    cellSeparation            = cellSeparation,
                    cellObstacleDistance      = cellObstacleDistance,
                    cellObstaclePositionIndex = cellObstaclePositionIndex,
                    cellTargetPistionIndex    = cellTargetPositionIndex,
                    cellCount                 = cellCount,
                    targetPositions           = targetPositions,
                    obstaclePositions         = obstaclePositions
                };
                var mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap,64,hashPositionsJobHandle);

                var steerJob = new Steer
                {
                    cellIndices               = nextCells.cellIndices,
                    settings                  = settings,
                    cellAlignment             = cellAlignment,
                    cellSeparation            = cellSeparation,
                    cellObstacleDistance      = cellObstacleDistance,
                    cellObstaclePositionIndex = cellObstaclePositionIndex,
                    cellTargetPistionIndex    = cellTargetPositionIndex,
                    cellCount                 = cellCount,
                    targetPositions           = targetPositions,
                    obstaclePositions         = obstaclePositions,
                    dt                        = Time.deltaTime,
                    positions                 = positions,
                    headings                  = headings,
                    transformMatrices         = transformMatrices
                };
                var steerJobHandle = steerJob.Schedule(boidCount, 64, mergeCellsJobHandle);
                    
                inputDeps = steerJobHandle;
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
                m_PrevCells[i].cellIndices.Dispose();
                m_PrevCells[i].hashMap.Dispose();
                m_PrevCells[i].cellAlignment.Dispose();
                m_PrevCells[i].cellSeparation.Dispose();
                m_PrevCells[i].cellObstacleDistance.Dispose();
                m_PrevCells[i].cellObstaclePositionIndex.Dispose();
                m_PrevCells[i].cellTargetPistionIndex.Dispose();
                m_PrevCells[i].cellCount.Dispose();
            }
        }
    }
}
