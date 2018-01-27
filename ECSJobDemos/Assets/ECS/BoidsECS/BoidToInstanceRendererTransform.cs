using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;

namespace BoidSimulations
{
#if true
    
    // Transforms BoidData simulation state to TransformMatrix
    // which is essentially a matrix used by the rendering system.
    [UpdateBefore(typeof(BoidSimulationSystem))]
    class BoidToInstanceRendererTransform : JobComponentSystem
    {
        struct Group
        {
            public ComponentDataArray<BoidData>                  boids;
            public ComponentDataArray<TransformMatrix> rendererTransforms;
        }

        [Inject] 
        Group m_Group;
        
        [ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
        struct TransformJob : IJobProcessComponentData<BoidData, TransformMatrix>
        {
            public void Execute(ref BoidData boid, ref TransformMatrix transformMatrix)
            {
                transformMatrix.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new TransformJob();
            return job.Schedule(m_Group.boids, m_Group.rendererTransforms, 16, inputDeps);
        }
    }

#else

    // This code does exactly the same as above.
    // But using IAutoComponentSystemJob effectively creates the component system code automatically,
    // so you have to only write the job and scheduling it is taken care of automatically.
    [ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
    struct BoidToInstanceRendererTransform : IJobProcessComponentData<BoidData, TransformMatrix>, IAutoComponentSystemJob
    {
        public void Prepare() { }

        public void Execute(ref BoidData boid, ref TransformMatrix transform)
        {
            transform.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
        }
    }
    
#endif
}