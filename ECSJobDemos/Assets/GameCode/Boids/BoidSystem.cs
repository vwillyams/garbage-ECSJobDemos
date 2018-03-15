using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.ECS.Utilities;

namespace Samples.Boids
{
    [UpdateBefore(typeof(TransformSystem))]
    public class BoidSystem : JobComponentSystem
    {
        private ComponentGroup  m_BoidGroup;
        private ComponentGroup  m_TargetGroup;
        private ComponentGroup  m_ObstacleGroup;
        private List<Boid>      m_UniqueTypes = new List<Boid>(10);
        private List<PrevCells> m_PrevCells = new List<PrevCells>();

        struct PrevCells
        {
            public int                          boidCount;
            public NativeMultiHashMap<int, int> hashMap;
            public NativeArray<Cell>            cells;
            public NativeArray<int>             cellIndices;
        }

        struct Cell
        {
            public float3 alignment;
            public float3 separation;
            public float  count;
            public float3 obstaclePosition;
            public float  obstacleDistance;
            public float3 targetPosition;
            public float  targetDistance;
        }

        [ComputeJobOptimization]
        struct HashPositions : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<Heading>  headings;
            public NativeMultiHashMap<int, int>.Concurrent hashMap;
            public NativeArray<Cell>                       cells;
            public float                                   cellRadius;

            public void Execute(int index)
            {
                var position = positions[index].Value;
                var hash = GridHash.Hash(position, cellRadius);
                hashMap.Add(hash, index);
                cells[index] = new Cell
                {
                    count      = 1,
                    alignment  = headings[index].Value,
                    separation = -position
                };
            }
        }

        [ComputeJobOptimization]
        struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            public NativeArray<Cell>                       cells;
            public NativeArray<int>                        cellIndices;
            [ReadOnly] public ComponentDataArray<Position> targetPositions;
            [ReadOnly] public ComponentDataArray<Position> obstaclePositions;
            
            void NearestPosition(ComponentDataArray<Position> targets, float3 position, out float3 nearestPosition, out float nearestDistance )
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
            

            public void Execute(int firstIndex, int index)
            {
                var cell = cells[firstIndex];
                
                cellIndices[index] = firstIndex;
                
                if (firstIndex == index)
                {
                    var position = cells[firstIndex].separation / -cells[firstIndex].count;
                    
                    NearestPosition(obstaclePositions,position,out cell.obstaclePosition,out cell.obstacleDistance);
                    NearestPosition(targetPositions,position,out cell.targetPosition,out cell.targetDistance);
                }
                else
                {
                    cell.count      += 1.0f;
                    cell.alignment  += cells[index].alignment;
                    cell.separation += cells[index].separation;
                }
                
                cells[firstIndex] = cell;
            }
        }

        [ComputeJobOptimization]
        struct Steer : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int>         cellIndices;
            [ReadOnly] public NativeArray<Cell>        cells;
            [ReadOnly] public Boid                     settings;
            public float                               dt;
            public ComponentDataArray<Position>        positions;
            public ComponentDataArray<Heading>         headings;
            public ComponentDataArray<TransformMatrix> transformMatrices;
            
            public void Execute(int index)
            {
                var forward                           = headings[index].Value;
                var position                          = positions[index].Value;
                var cellIndex                         = cellIndices[index];
                var neighborCount                     = cells[cellIndex].count;
                var cellAlignment                     = cells[cellIndex].alignment;
                var cellSeparation                    = cells[cellIndex].separation;
                var nearestObstacleDistance           = cells[cellIndex].obstacleDistance;
                var nearestObstaclePosition           = cells[cellIndex].obstaclePosition;
                var nearestTargetPosition             = cells[cellIndex].targetPosition;
                
                var obstacleSteering                  = position - nearestObstaclePosition;
                var avoidObstacleHeading              = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                var targetHeading                     = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPosition - position);
                var nearestObstacleDistanceFromRadius = nearestObstacleDistance - settings.obstacleAversionDistance;
                
                var alignmentResult                   = settings.alignmentWeight * math_experimental.normalizeSafe((cellAlignment/neighborCount)-forward);
                var separationResult                  = settings.separationWeight * math_experimental.normalizeSafe((position * neighborCount) + cellSeparation);
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
            var targetPositions = m_TargetGroup.GetComponentDataArray<Position>();
            
            // Ingore typeIndex 0, can't use the default for anything meaningful.
            for (int typeIndex = 1; typeIndex < m_UniqueTypes.Count; typeIndex++)
            {
                var settings = m_UniqueTypes[typeIndex];
                m_BoidGroup.SetFilter(settings);
                
                var positions         = m_BoidGroup.GetComponentDataArray<Position>();
                var headings          = m_BoidGroup.GetComponentDataArray<Heading>();
                var transformMatrices = m_BoidGroup.GetComponentDataArray<TransformMatrix>();

                var cacheIndex  = typeIndex - 1;
                var boidCount   = positions.Length;
                var cells       = new NativeArray<Cell>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                var hashMap     = new NativeMultiHashMap<int,int>(boidCount,Allocator.TempJob);

                var nextCells = new PrevCells
                {
                    boidCount = boidCount,
                    cells = cells,
                    cellIndices = cellIndices,
                    hashMap = hashMap
                };
                
                if (cacheIndex > (m_PrevCells.Count - 1))
                {
                    m_PrevCells.Add(nextCells);
                }
                else
                {
                    m_PrevCells[cacheIndex].cells.Dispose();
                    m_PrevCells[cacheIndex].cellIndices.Dispose();
                    m_PrevCells[cacheIndex].hashMap.Dispose();
                }
                m_PrevCells[cacheIndex] = nextCells;

                var hashPositionsJob = new HashPositions
                {
                    positions = positions,
                    headings = headings,
                    hashMap = hashMap,
                    cells = cells,
                    cellRadius = settings.cellRadius
                };
                var hashPositionsJobHandle = hashPositionsJob.Schedule(boidCount, 64, inputDeps);

                var cellsJob = new MergeCells
                {
                    cells = cells,
                    cellIndices = cellIndices,
                    targetPositions = targetPositions,
                    obstaclePositions = obstaclePositions
                };
                var cellsJobHandle = cellsJob.Schedule(hashMap,64,hashPositionsJobHandle);

                var steerJob = new Steer
                {
                    cellIndices = nextCells.cellIndices,
                    cells = nextCells.cells,
                    settings = settings,
                    dt = Time.deltaTime,
                    positions = positions,
                    headings = headings,
                    transformMatrices = transformMatrices
                };
                var steerJobHandle = steerJob.Schedule(boidCount, 64, cellsJobHandle);
                    
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
                m_PrevCells[i].cells.Dispose();
                m_PrevCells[i].cellIndices.Dispose();
                m_PrevCells[i].hashMap.Dispose();
            }
        }
    }
}
