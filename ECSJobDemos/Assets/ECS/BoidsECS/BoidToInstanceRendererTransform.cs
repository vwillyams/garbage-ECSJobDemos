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
    // This file contains all the different ways you can iterate over entities.
    // Our current recommendation is to use the last two
    // BoidToInstanceRendererTransform_IJobProcessComponentData or BoidToInstanceRendererTransform whenever possible.


    // Uses GetEntities<Group> to foreach iterate over all entities
    // and produce a matrix from the BoidData state. Running all on the main thread.
    [DisableAutoCreation]
    class BoidToInstanceRendererTransform_GetEntities : ComponentSystem
    {
        unsafe struct Group
        {
            [ReadOnly] 
            public BoidData*        Boid;
            public TransformMatrix* Transform;
        }

        unsafe protected override void OnUpdate()
        {
            // GetEntities<Group> lets us iterates over all entities
            // that have both a BoidData and TransformMatrix components attached.
            foreach (var e in GetEntities<Group>())
            {
                var boid = e.Boid;
                e.Transform->matrix = matrix_math_util.LookRotationToMatrix(boid->position, boid->forward, new float3(0, 1, 0));
            }
        }
    }

    // Uses injection of a ComponentDataArray group to
    // iterate over all entities with matching components on the main thread.    
    [DisableAutoCreation]
    class BoidToInstanceRendererTransform_ComponentDataArray : ComponentSystem
    {
        struct Group
        {
            [ReadOnly]
            public ComponentDataArray<BoidData>        Boids;
            public ComponentDataArray<TransformMatrix> RendererTransforms;
            public int                                 Length;
        }

        // [Inject] creates a ComponentGroup, setting up the two ComponentDataArrays so
        // that we can iterate over all entities containing both BoidData & TransformMatrix.
        [Inject] 
        Group m_Group;

        protected override void OnUpdate()
        {
            // We iterate over the all injected entities containing BoidData & TransformMatrix Components
            for (var i = 0; i != m_Group.Length; i++)
            {
                var boid = m_Group.Boids[i];
                
                TransformMatrix transform;
                transform.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
                m_Group.RendererTransforms[i] = transform;
            }
        }
    }

    // Uses injection of a ComponentDataArray group to
    // iterate over all entities with matching components.
    // IJobParallelFor executes the code on multiple cores.
    [DisableAutoCreation]
    class BoidToInstanceRendererTransform_ParallelForJob_ComponentDataArray  : JobComponentSystem
    {
        struct Group
        {
            [ReadOnly]
            public ComponentDataArray<BoidData>        Boids;
            public ComponentDataArray<TransformMatrix> RendererTransforms;
            public int                                 Length;
        }

        // [Inject] creates a ComponentGroup, setting up the two ComponentDataArrays so
        // that we can iterate over all entities containing both BoidData & TransformMatrix.    
        [Inject] 
        Group m_Group;
        
        // We use IJobParallelFor to execute the jobs in parallel on multiple cores.
        [ComputeJobOptimization]
        struct TransformJob : IJobParallelFor
        {
            [ReadOnly]
            public ComponentDataArray<BoidData>        Boids;
            public ComponentDataArray<TransformMatrix> TransformMatrices;
            
            public void Execute(int index)
            {
                var boid = Boids[index];
                
                TransformMatrix transform;
                transform.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
                
                TransformMatrices[index] = transform;
            }
        }
        
        // We derive from JobComponentSystem, as a result ECS hands us the required dependencies for our jobs.
        //
        // This is possible because we declare using the Group struct, which components we read & write from.
        // Also declares what data is being read & written to in this ComponentSystem by declaring it in the Group struct.
        // Because it is declared the JobComponentSystem can give us a Job dependency, which contains all previously scheduled
        // jobs that write to any BoidData or RendererTransforms.
        // We also have to return the dependency so any job we schedule will get registered against the types for the next System that might run
        // This approach means:
        // * No waiting on main thread, just scheduling jobs with dependencies (Jobs only start when dependencies have completed)
        // * Dependencies are figured out automatically for us, so we can write modular multithreaded code
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new TransformJob() { Boids = m_Group.Boids, TransformMatrices = m_Group.RendererTransforms };
            return job.Schedule(m_Group.Length, 16, inputDeps);
        }
    }
    
    // Using IJobProcessComponentData to iterate of all entities matching the required component types.
    // Processing of entities happens in parallel. The Main thread only schedules jobs.
    [DisableAutoCreation]
    class BoidToInstanceRendererTransform_IJobProcessComponentData : JobComponentSystem
    {
        struct Group
        {
            [ReadOnly]
            public ComponentDataArray<BoidData>        Boids;
            public ComponentDataArray<TransformMatrix> RendererTransforms;
        }

        // [Inject] creates a ComponentGroup, setting up the two ComponentDataArrays so
        // that we can iterate over all entities containing both BoidData & TransformMatrix.    
        [Inject] 
        Group m_Group;
        
        // Instead of IJobParallelFor, we use IJobProcessComponentData
        // It is more efficient than IJobParallelFor and more convenient
        // * ComponentDataArray has one early out branch per index lookup
        // * IJobProcessComponentData innerloop does a straight BoidData* array iteration, with zero checks. 
        [ComputeJobOptimization]
        struct TransformJob : IJobProcessComponentData<BoidData, TransformMatrix>
        {
            public void Execute(ref BoidData boid, ref TransformMatrix transformMatrix)
            {
                transformMatrix.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
            }
        }

        // We derive from JobComponentSystem, as a result ECS hands us the required dependencies for our jobs.
        //
        // This is possible because we declare using the Group struct, which components we read & write from.
        // Also declares what data is being read & written to in this ComponentSystem by declaring it in the Group struct.
        // Because it is declared the JobComponentSystem can give us a Job dependency, which contains all previously scheduled
        // jobs that write to any BoidData or RendererTransforms.
        // We also have to return the dependency so any job we schedule will get registered against the types for the next System that might run
        // This approach means:
        // * No waiting on main thread, just scheduling jobs with dependencies (Jobs only start when dependencies have completed)
        // * Dependencies are figured out automatically for us, so we can write modular multithreaded code
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new TransformJob();
            return job.Schedule(m_Group.Boids, m_Group.RendererTransforms, 16, inputDeps);
        }
    }


    // We also use IJobProcessComponentData (Same as above)
    // But we are using IAutoComponentSystemJob.
    // 
    // Essentially we tell the ECS that it should create a JobComponentSystem for us that will automatically schedule the job every frame,
    // 
    // Effectively it's a literally the same BoidToInstanceRendererTransform_IJobProcessComponentData,
    // just with a lot less code and the system code being done for us automatically.
    [ComputeJobOptimization]
    struct BoidToInstanceRendererTransform : IJobProcessComponentData<BoidData, TransformMatrix>, IAutoComponentSystemJob
    {
        // This is invoked on the main thread.
        // You could use it to store the deltaTime before scheduling the job here.
        public void Prepare() { }

        public void Execute(ref BoidData boid, ref TransformMatrix transform)
        {
            transform.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
        }
    }
}