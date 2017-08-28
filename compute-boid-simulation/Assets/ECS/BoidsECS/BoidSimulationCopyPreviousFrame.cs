using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.ECS;

namespace BoidSimulations
{
	[UpdateAfter("FixedUpdate.DirectorFixedUpdatePostPhysics")]
	public class BoidSimulationCopyPreviousFrame : JobComponentSystem
	{
		[InjectTuples(0)]
		ComponentDataArray<BoidData> 				m_BoidDataT0;

		[InjectTuples(0)]
		ComponentDataArray<BoidDataInterpolation>   m_BoidDataT1;

		struct CopyJob : IJobParallelFor
		{
			[ReadOnly]
			public ComponentDataArray<BoidData>					boids;

			public ComponentDataArray<BoidDataInterpolation> 	interpolationData;

			public void Execute(int index)
			{
				BoidDataInterpolation data;
				data.previousFrame = interpolationData[index].thisFrame;
				data.thisFrame = boids[index];

				interpolationData[index] = data;
			}
		}

		override protected void OnUpdate()
		{
			base.OnUpdate ();

			CopyJob copyJob;
			copyJob.boids = m_BoidDataT0;
			copyJob.interpolationData = m_BoidDataT1;

			copyJob.Schedule (m_BoidDataT0.Length, 512, GetDependency()).Complete();
		}
	}
}