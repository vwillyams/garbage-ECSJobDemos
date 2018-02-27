﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace UnityEngine.ECS.SimpleRotation
{
    public class HeadingSystem : JobComponentSystem
    {
        struct HeadingsGroup
        {
            public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<Heading> headings;
            public int Length;
        }

        [Inject] private HeadingsGroup m_HeadingsGroup;
        
        struct LocalHeadingsGroup
        {
            public ComponentDataArray<LocalRotation> rotations;
            [ReadOnly] public ComponentDataArray<LocalHeading> headings;
            public int Length;
        }

        [Inject] private LocalHeadingsGroup m_LocalHeadingsGroup;
        
        [ComputeJobOptimization]
        struct RotationFromHeading : IJobParallelFor
        {
            public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<Heading> headings;
        
            public void Execute(int i)
            {
                var heading = headings[i].Value;
                var rotation = math.lookRotationToQuaternion(heading, math.up());
                rotations[i] = new Rotation { Value = rotation };
            }
        }
        
        [ComputeJobOptimization]
        struct LocalRotationFromLocalHeading : IJobParallelFor
        {
            public ComponentDataArray<LocalRotation> rotations;
            [ReadOnly] public ComponentDataArray<LocalHeading> headings;
        
            public void Execute(int i)
            {
                rotations[i] = new LocalRotation { Value = math.lookRotationToQuaternion(headings[i].Value, math.up()) };
            }
        }
    
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationFromHeadingJob = new RotationFromHeading
            {
                rotations = m_HeadingsGroup.rotations,
                headings = m_HeadingsGroup.headings,
            };
            var rotationFromHeadingJobHandle = rotationFromHeadingJob.Schedule(m_HeadingsGroup.Length, 64, inputDeps);
            
            var localRotationFromLocalHeadingJob = new LocalRotationFromLocalHeading
            {
                rotations = m_LocalHeadingsGroup.rotations,
                headings = m_LocalHeadingsGroup.headings,
            };
            var localRotationFromLocalHeadingJobHandle = localRotationFromLocalHeadingJob.Schedule(m_LocalHeadingsGroup.Length, 64, inputDeps);
            
            return JobHandle.CombineDependencies(rotationFromHeadingJobHandle,localRotationFromLocalHeadingJobHandle);
        } 
    }
}