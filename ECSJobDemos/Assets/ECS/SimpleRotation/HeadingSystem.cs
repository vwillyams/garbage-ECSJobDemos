using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class HeadingSystem : JobComponentSystem
    {
        struct HeadingsGroup
        {
            public ComponentDataArray<Rotation> rotations;
            [ReadOnly]
            public ComponentDataArray<Heading> headings;
            public int Length;
        }

        [Inject] private HeadingsGroup m_HeadingsGroup;
    
        [ComputeJobOptimization]
        struct HeadingRotation : IJobParallelFor
        {
            public ComponentDataArray<Rotation> rotations;
            [ReadOnly]
            public ComponentDataArray<Heading> headings;
        
            public void Execute(int i)
            {
                rotations[i] = new Rotation
                {
                    rotation = math.lookRotationToQuaternion(headings[i].forward, math.up())
                };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var headingRotationJob = new HeadingRotation
            {
                rotations = m_HeadingsGroup.rotations,
                headings = m_HeadingsGroup.headings,
            };
            return headingRotationJob.Schedule(m_HeadingsGroup.Length, 64, inputDeps);
        } 
    }
}
