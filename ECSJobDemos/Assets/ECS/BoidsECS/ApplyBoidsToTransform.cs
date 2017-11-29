using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.ECS;
using UnityEngine.Jobs;


namespace BoidSimulations
{
	[UpdateAfter(typeof(BoidSimulationSystem))]
	class BoidsToTransformSystem : JobComponentSystem
	{
		struct Group
		{
			[ReadOnly]
			public ComponentDataArray<BoidData> 	boids;

			public TransformAccessArray 		    transforms;
		}

		[InjectComponentGroup] Group m_Boids;

		struct WriteBoidsToTransformsJob : IJobParallelForTransform
		{
			[ReadOnly]
			public ComponentDataArray<BoidData> 	boidData;

			public void Execute(int index, TransformAccess transform)
			{
                var boid = boidData[index];

				transform.position = boid.position;
				transform.rotation = Quaternion.LookRotation(boid.forward);
			}
		}

		public override JobHandle OnUpdate(JobHandle inputDeps)
		{
			WriteBoidsToTransformsJob writeJob;
			writeJob.boidData = m_Boids.boids;

			return writeJob.Schedule(m_Boids.transforms, inputDeps);
		}
	}
}

struct Blah : IComponentData
{
    
}