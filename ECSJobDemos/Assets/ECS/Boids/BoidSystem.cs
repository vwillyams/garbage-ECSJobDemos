using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;
using UnityEngine.XR.WSA;

namespace UnityEngine.ECS.Boids
{
    public class BoidSystem : JobComponentSystem
    {
        NativeMultiHashMap<int, int> 		 m_Cells;
        NativeArray<int3> 					 m_CellOffsetsTable;

        struct BoidSettingsGroup
        {
            [ReadOnly] public ComponentDataArray<BoidSettings> settings;
            public int Length;
        }

        [Inject] private BoidSettingsGroup m_BoidSettingsGroup;

        struct BoidGroup
        {
            [ReadOnly] public ComponentDataArray<Boid>                boid; 
            [ReadOnly] public ComponentDataArray<TransformPosition>   positions;
            public ComponentDataArray<TransformRotation>              rotations;
            public int Length;
        }

        [Inject] private BoidGroup m_BoidGroup;
        
        [ComputeJobOptimization]
        struct HashBoidLocations : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            public NativeMultiHashMap<int, int>.Concurrent 			cells;
            public float 											cellRadius;

            public void Execute(int index)
            {
                var hash = HashUtility.Hash(positions[index].position, cellRadius);
                cells.Add(hash, index);
            }
        }
        
        [ComputeJobOptimization]
        struct Steer : IJob
        {
            [ReadOnly] public ComponentDataArray<TransformPosition>   positions;
            public ComponentDataArray<TransformRotation>              rotations;
            [ReadOnly] public NativeMultiHashMap<int, int>            cells;
            [ReadOnly] public NativeArray<int3> 					  cellOffsetsTable;
            public BoidSettings settings;
            public float dt;
            
            void CalculateSeparationAndAlignment(int index, out float3 separationSteering, out float3 alignmentSteering )
            {
                var position = positions[index].position;
                var forward = math.forward(rotations[index].rotation);
                
                separationSteering = new float3(0);
                alignmentSteering = new float3(0);
                
                int hash;
                int3 gridPos = HashUtility.Quantize(position, settings.cellRadius);
                for (int oi = 0; oi < 7; oi++)
                {
                    var gridOffset = cellOffsetsTable[oi];

                    hash = HashUtility.Hash(gridPos + gridOffset);
                    int i;

                    NativeMultiHashMapIterator<int> iterator;
                    bool found = cells.TryGetFirstValue(hash, out i, out iterator);
                    int neighbors = 0;
                    while (found && neighbors < 2)        // limit neighbors to help initial hiccup due to all boids starting from same point
                    {
                        if (i == index)
                        {
                            found = cells.TryGetNextValue(out i, ref iterator);
                            continue;
                        }
                        neighbors++;

                        var otherPosition = positions[i].position;
                        var otherForward = math.forward(rotations[i].rotation);

                        // add in steering contribution
                        // (opposite of the offset direction, divided once by distance
                        // to normalize, divided another time to get 1/d falloff)
                        var offset = otherPosition - (position + forward * 0.5f);

                        // should we have sqrLength?
                        var distanceSquared = math.dot(offset, offset);
                        separationSteering += (offset / -distanceSquared);

                        // accumulate sum of neighbor's heading
                        alignmentSteering += otherForward;

                        found = cells.TryGetNextValue(out i, ref iterator);
                    }
                }

                separationSteering = math_experimental.normalizeSafe(separationSteering);
                alignmentSteering = math_experimental.normalizeSafe(alignmentSteering);
            }

            public void Execute()
            {
                for (int index = 0; index < rotations.Length; index++)
                {
                    var rotation = rotations[index].rotation;
                    
                    float3 alignmentSteering;
                    float3 separationSteering;

                    CalculateSeparationAndAlignment(index, out alignmentSteering, out separationSteering);

                    var steer = (alignmentSteering * settings.alignmentWeight) +
                                (separationSteering * settings.separationWeight);
                    math_experimental.normalizeSafe(steer);

                    var forward = math_experimental.normalizeSafe(math.forward(rotation) + steer * dt * Mathf.Deg2Rad * settings.rotationalSpeed);
                    var forwardRotation = math.lookRotationToQuaternion(forward, math.up());
                    
                    rotations[index] = new TransformRotation
                    {
                        rotation = forwardRotation
                    };
                }
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_BoidSettingsGroup.Length == 0)
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

            var steerJob = new Steer
            {
                positions = m_BoidGroup.positions,
                rotations = m_BoidGroup.rotations,
                cells = m_Cells,
                settings = settings,
                cellOffsetsTable = m_CellOffsetsTable,
                dt = Time.deltaTime
            };

            var steerJobHandle = steerJob.Schedule(hashBoidLocationsJobHandle);
                
            return steerJobHandle;
        }
        
        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            m_Cells = new NativeMultiHashMap<int, int>(capacity, Allocator.Persistent);
            m_CellOffsetsTable = new NativeArray<int3>(HashUtility.cellOffsets, Allocator.Persistent);
        }

        protected override void OnDestroyManager()
        {
            base.OnDestroyManager();
            m_Cells.Dispose ();
            m_CellOffsetsTable.Dispose();
        }

    }
}
