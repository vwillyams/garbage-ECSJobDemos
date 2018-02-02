﻿using Boo.Lang;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.Transform
{
    public class TransformSystem : JobComponentSystem
    {
        struct WorldTransGroup
        {
            public ComponentDataArray<TransformMatrix> matrices;
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            [ReadOnly] public SubtractiveComponent<TransformRotation> rotations;
            public int Length;
        }
        
        [Inject] private WorldTransGroup m_WorldTransGroup;
        
        struct WorldRotTransGroup
        {
            public ComponentDataArray<TransformMatrix> matrices;
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            [ReadOnly] public ComponentDataArray<TransformRotation> rotations;
            public int Length;
        }
        
        [Inject] private WorldRotTransGroup m_WorldRotTransGroup;
    
        [ComputeJobOptimization]
        struct WorldTransToMatrix : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            public ComponentDataArray<TransformMatrix> matrices;
        
            public void Execute(int i)
            {
                var position = positions[i].position;
                matrices[i] = new TransformMatrix
                {
                    matrix = math.translate(position)
                };
            }
        }
        
        [ComputeJobOptimization]
        struct WorldRotTransToMatrix : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<TransformPosition> positions;
            [ReadOnly] public ComponentDataArray<TransformRotation> rotations;
            public ComponentDataArray<TransformMatrix> matrices;
        
            public void Execute(int i)
            {
                float3 position = positions[i].position;
                matrices[i] = new TransformMatrix
                {
                    matrix = math.rottrans( math.normalize(rotations[i].rotation), position )
                };
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var worldTransToMatrixJob = new WorldTransToMatrix();
            worldTransToMatrixJob.positions = m_WorldTransGroup.positions;
            worldTransToMatrixJob.matrices = m_WorldTransGroup.matrices;
            var worldTransToMatrixJobHandle = worldTransToMatrixJob.Schedule(m_WorldTransGroup.Length, 64, inputDeps);
            
            var worldRotTransToMatrixJob = new WorldRotTransToMatrix();
            worldRotTransToMatrixJob.positions = m_WorldRotTransGroup.positions;
            worldRotTransToMatrixJob.matrices = m_WorldRotTransGroup.matrices;
            worldRotTransToMatrixJob.rotations = m_WorldRotTransGroup.rotations;
            // var worldRotTransToMatrixJobHandle = worldRotTransToMatrixJob.Schedule(m_WorldRotTransGroup.Length, 64, inputDeps);
            // return JobHandle.CombineDependencies(worldRotTransToMatrixJobHandle, worldTransToMatrixJobHandle);
            return worldRotTransToMatrixJob.Schedule(m_WorldRotTransGroup.Length, 64, worldTransToMatrixJobHandle);
        } 
    }
}
