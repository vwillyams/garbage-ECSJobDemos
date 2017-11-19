using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.ECS;
using UnityEngine.Jobs;


namespace RotatorSamples
{
	[Serializable]
	public struct RotationSpeed : IComponentData
	{
		public float speed;
	}

	public class RotationSpeedDataComponent : ComponentDataWrapper<RotationSpeed> { }

#if false

	// Single thread
	[UpdateAfter(typeof(DamageSystem))]
	public class SystemRotator : ComponentSystem
	{
		struct Group
		{
			public TransformAccessArray    				transforms;
			public ComponentDataArray<RotationSpeed>    rotators;
			public int 								    Length;
		}

		[InjectComponentGroup]
		Group m_Rotators;

		override public void OnUpdate()
		{
			base.OnUpdate ();

			float dt = Time.deltaTime;
			for (int i = 0; i != m_Rotators.Length;i++)
			{
				m_Rotators.transforms[i].rotation = m_Rotators.transforms[i].rotation * Quaternion.AngleAxis(dt * m_Rotators.rotators[i].speed, Vector3.up);
			}
		}
	}
		
#else

    // Multithread
    [UpdateAfter(typeof(DamageSystem))]
	public class SystemRotator : JobComponentSystem
	{
		struct Group
		{
			public TransformAccessArray    				transforms;
			public ComponentDataArray<RotationSpeed>    rotators;
		}

		[InjectComponentGroup]
		Group m_Rotators;
		
		public override void OnUpdate()
		{
			base.OnUpdate ();

			var job = new Job();
			job.dt = Time.deltaTime;
			job.rotators = m_Rotators.rotators;

            AddDependency(job.Schedule(m_Rotators.transforms, GetDependency()));
		}

		struct Job : IJobParallelForTransform
		{
			public float dt;
            [ReadOnly]
			public ComponentDataArray<RotationSpeed>      rotators;

			public void Execute(int i, TransformAccess transform)
			{
				transform.rotation = transform.rotation * Quaternion.AngleAxis(dt * rotators[i].speed, Vector3.up);
			}
		}
	}

	#endif
}