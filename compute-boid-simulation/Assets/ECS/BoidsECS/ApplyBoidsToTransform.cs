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
        [ReadOnly]
        ComponentDataArray<BoidData> 	m_BoidData;

		[InjectTuples]
		TransformAccessArray 		    m_BoidTransforms;

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

		override public void OnUpdate()
		{
			base.OnUpdate ();

			WriteBoidsToTransformsJob writeJob;
			writeJob.boidData = m_BoidData;

			AddDependency(writeJob.Schedule (m_BoidTransforms, GetDependency()));
		}
	}
}