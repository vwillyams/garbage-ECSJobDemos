using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.ECS;

namespace BoidSimulations
{

	[UpdateAfter(typeof(BoidSimulationSystem))]
	class BoidsToTransformSystem : JobComponentSystem
	{
		[InjectTuples]
		ComponentDataArray<BoidData> 				m_BoidData;

		[InjectTuples]
		TransformAccessArray 						m_BoidTransforms;

		struct WriteBoidsToTransformsJob : IJobParallelForTransform
		{
			[ReadOnly]
			public ComponentDataArray<BoidData>		boids;

			public void Execute(int index, TransformAccess transform)
			{
				var boid = boids[index];
				transform.position = boid.position;
				transform.rotation = Quaternion.LookRotation(boid.forward);
			}
		}

		override protected void OnUpdate()
		{
			base.OnUpdate ();

			WriteBoidsToTransformsJob writeJob;
			writeJob.boids = m_BoidData;
			AddDependency(writeJob.Schedule (m_BoidTransforms, GetDependency()));
		}
	}
}