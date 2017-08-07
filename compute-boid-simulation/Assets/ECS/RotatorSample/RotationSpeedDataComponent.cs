using UnityEngine;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;
using System;
using ECS;

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
[UnityEngine.ExecuteInEditMode]
public class RotationSpeedDataComponent : ComponentDataWrapper<RotationSpeed> { }


//@TODO: struct with Length returning itself infinite recursion crashes editor instead of exception...

#if false

// Single thread
[UpdateAfter(typeof(DamageSystem))]
public class RotatingSystem : ComponentSystem
{
	// NOTE: InjectTuples scans all [InjectTuples] in the class
	// and returns the union of objects that have both Transform and LightRotator
	[InjectTuples]
	public ComponentArray<Transform> m_Transforms;

	[InjectTuples]
	public ComponentDataArray<RotationSpeed>        m_Rotators;


	override protected void OnUpdate()
	{
		base.OnUpdate ();

		float dt = Time.deltaTime;
		for (int i = 0; i != m_Transforms.Length;i++)
		{
			m_Transforms[i].rotation = m_Transforms[i].rotation * Quaternion.AngleAxis(dt * m_Rotators[i].speed, Vector3.up);
		}
	}
}
	
#else
// Multithread
[UpdateAfter(typeof(DamageSystem))]
public class SystemRotator : JobComponentSystem
{
	// NOTE: InjectTuples scans all [InjectTuples] in the class
	// and returns the union of objects that have both Transform and LightRotator

	[InjectTuples]
	public TransformAccessArray                m_Transforms;

	[InjectTuples]
	// @TODO: support some way of expressing read only for manager data...
	public ComponentDataArray<RotationSpeed>    m_Rotators;

	protected override void OnUpdate()
	{
		base.OnUpdate ();

		var job = new Job();
		job.dt = Time.deltaTime;
		job.rotators = m_Rotators;

		AddDependency(job.Schedule(m_Transforms, GetDependency ()));
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