using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class LocalHeadingSystem : JobComponentSystem
    {
        struct LocalHeadingsGroup
        {
            public ComponentDataArray<LocalRotation> rotations;
            [ReadOnly]
            public ComponentDataArray<LocalHeading> headings;
            public int Length;
        }

        [Inject] private LocalHeadingsGroup m_LocalHeadingsGroup;
    
        [ComputeJobOptimization]
        struct LocalHeadingLocalRotation : IJobParallelFor
        {
            public ComponentDataArray<LocalRotation> rotations;
            [ReadOnly]
            public ComponentDataArray<LocalHeading> headings;
        
            public void Execute(int i)
            {
                rotations[i] = new LocalRotation
                {
                    rotation = math.lookRotationToQuaternion(headings[i].forward, math.up())
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var headingLocalRotationJob = new LocalHeadingLocalRotation
            {
                rotations = m_LocalHeadingsGroup.rotations,
                headings = m_LocalHeadingsGroup.headings,
            };
            return headingLocalRotationJob.Schedule(m_LocalHeadingsGroup.Length, 64, inputDeps);
        } 
    }
}
