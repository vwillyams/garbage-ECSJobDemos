using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using System.Collections.Generic;


namespace ECS
{
	[UpdateAfter(typeof(BoidSimulationSystem))]
	class BoidsToTransformSystem : JobComponentSystem
	{
		[InjectTuples(0)]
		ComponentDataArray<BoidData> 				m_BoidData;

		[InjectTuples(0)]
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

		override protected void OnCreateManager(int capacity)
		{
			base.OnCreateManager (capacity);
		}

		override protected void OnUpdate()
		{
			base.OnUpdate ();

			//@TODO: This shouldn't really be necessary right now
			//       it is needed because job chaining is not properly supported...
			if (m_BoidTransforms.Length == 0)
				return;

			WriteBoidsToTransformsJob writeJob;
			writeJob.boids = m_BoidData;
			AddDependency(writeJob.Schedule (m_BoidTransforms, GetDependency()));
		}
	}
}

