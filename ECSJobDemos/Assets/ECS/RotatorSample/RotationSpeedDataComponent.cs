using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.ECS;
using UnityEngine.Jobs;


namespace RotatorSamples
{
	// New light weight component is a struct.
	// The data is stored in a NativeArray owned by a LightWeightComponentManager<>
	// 
	// * Data is stored in tightly packed array (Good for performance and also allows for safe jobification)
	// * Allows for light weight components to live without their game object, 
	//   enabling massive scale lightweight simulation (Think 2M instances in City Skylines)
	[Serializable]
	public struct RotationSpeed : IComponentData
	{
		public float speed;

		public RotationSpeed (float speed) { this.speed = speed; }
	}


	// @TODO: This whole  wraper component should be unnecessary and just handled on the C++ side.
	// A new GameObject light weight component integration would make the inspector look like it is a full component
	// while the data is actually stored in a LightWeightComponentManager<>.
	// Effectively the game object simply stores an index to the LightWeightComponentManager<>.
	public class RotationSpeedDataComponent : ComponentDataWrapper<RotationSpeed> { }


    //@TODO: struct with Length returning itself infinite recursion crashes editor instead of exception...

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