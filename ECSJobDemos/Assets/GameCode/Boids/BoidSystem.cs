﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine.ECS.SimpleRotation;
using Unity.Transforms;
using UnityEngine.ECS.Utilities;

namespace UnityEngine.ECS.Boids
{
    [DisableSystemWhenEmpty]
    public class BoidSystem : JobComponentSystem
    {
        // #todo Should be Allocator.TempJpb once NativeMultiHashMap can DeallocateOnJobCompletion
        List<NativeMultiHashMap<int, int>> 	 m_Cells; 
        
        NativeArray<int3> 					 m_CellOffsetsTable;
        NativeArray<float>                   m_Bias;

        ComponentGroup                       m_MainGroup;
        List<Boid> m_UniqueTypes = new List<Boid>(10);

        [ComputeJobOptimization]
        struct HashBoidLocations : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Position> positions;
            public NativeMultiHashMap<int, int>.Concurrent cells;
            public float 				     			   cellRadius;

            public void Execute(int index)
            {
                var hash = GridHash.Hash(positions[index].Value, cellRadius);
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
                results[index] = positions[index].Value;
            }
        }

        [ComputeJobOptimization]
        struct CopyHeading : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Heading> headings;
            public NativeArray<float3> results;

            public void Execute(int index)
            {
                results[index] = headings[index].Value;
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
                var averageHeading = new float3(0);
                var hash = GridHash.Hash(position, settings.cellRadius);

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

                    var offset = position - otherPosition;

                    separationSteering += offset / math.length(offset);
                    averageHeading += otherForward;
                        
                    found = cells.TryGetNextValue(out i, ref iterator);
                }

                separationResults[index] = separationSteering;
                alignmentResults[index] = (averageHeading / neighbors) - forward;
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
                var forward = headings[index].Value;
                var nearestObstaclePosition = nearestObstaclePositions[index].Value;
                var obstacleSteering = (position - nearestObstaclePosition);
                if (math.length(obstacleSteering) < settings.obstacleAversionDistance)
                {
                    var s3 = (nearestObstaclePosition + math_experimental.normalizeSafe(obstacleSteering) * settings.obstacleAversionDistance)-position;
                    var steer = math_experimental.normalizeSafe(forward + dt*(s3-forward));
                    headings[index] = new Heading { Value = steer };
                }
                else
                {
                    var s0 = settings.alignmentWeight * math_experimental.normalizeSafe(alignmentSteering[index]);
                    var s1 = settings.separationWeight * math_experimental.normalizeSafe(separationSteering[index]);
                    var s2 = settings.targetWeight * math_experimental.normalizeSafe(nearestTargetPositions[index].Value - position);
                    var s3 = math_experimental.normalizeSafe(s0 + s1 + s2);
                    var steer = math_experimental.normalizeSafe(forward + dt*(s3-forward));
                    headings[index] = new Heading { Value = steer };
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityManager.GetAllUniqueSharedComponentDatas(m_UniqueTypes);

            for (int typeIndex = 0; typeIndex < m_UniqueTypes.Count; typeIndex++)
            {
                var settings = m_UniqueTypes[typeIndex];
                var group = m_MainGroup.GetVariation(settings);
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
            m_UniqueTypes.Clear();

            // The return value only applies to jobs working with injected components
            return inputDeps;
        }

        protected override void OnCreateManager(int capacity)
        {
            m_Cells = new List<NativeMultiHashMap<int, int>>();
            m_CellOffsetsTable = new NativeArray<int3>(GridHash.cellOffsets, Allocator.Persistent);
            m_Bias = new NativeArray<float>(1024,Allocator.Persistent);
            for (int i = 0; i < 1024; i++)
            {
                m_Bias[i] = Random.Range(0.5f, 0.6f);
            }
            
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
                m_Cells[i].Dispose ();
            }
            m_CellOffsetsTable.Dispose();
            m_Bias.Dispose();
        }

    }
}
