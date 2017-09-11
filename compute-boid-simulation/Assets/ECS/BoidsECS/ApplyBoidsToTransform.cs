using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.ECS;


namespace BoidSimulations
{
	//[UpdateAfter(typeof(BoidSimulationSystem))]
	class BoidsToTransformSystem : JobComponentSystem
	{
		[InjectTuples]
		ComponentDataArray<BoidDataInterpolation> 	m_BoidData;

		[InjectTuples]
		TransformAccessArray 						m_BoidTransforms;

		struct WriteBoidsToTransformsJob : IJobParallelForTransform
		{
			[ReadOnly]
			public ComponentDataArray<BoidDataInterpolation> 	boidData;

			public float interpolation;

			public void Execute(int index, TransformAccess transform)
			{
				var boid = boidData[index];
				transform.position = math.lerp(boid.previousFrame.position, boid.thisFrame.position, interpolation);

				float3 lerped = math.lerp (boid.previousFrame.forward, boid.thisFrame.forward, interpolation);
				if (math.dot(lerped, lerped) > 0.00001F)
					transform.rotation = Quaternion.LookRotation(lerped);


				//transform.position = boid.position;
				//transform.rotation = Quaternion.LookRotation(boid.forward);
			}
		}

		override public void OnUpdate()
		{
			base.OnUpdate ();

			WriteBoidsToTransformsJob writeJob;
			writeJob.boidData = m_BoidData;
			writeJob.interpolation = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
			writeJob.interpolation = math.saturate (writeJob.interpolation);

			AddDependency(writeJob.Schedule (m_BoidTransforms, GetDependency()));
		}
	}
}