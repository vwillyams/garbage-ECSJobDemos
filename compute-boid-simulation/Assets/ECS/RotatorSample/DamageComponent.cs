using UnityEngine;
using UnityEngine.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;
using System;
using UnityEngine.ECS;

namespace RotatorSamples
{
	[Serializable]
	public struct Damage : IComponentData//IComponentData
	{
		public float radius;
		public float slowdownPerSecond;
	}

	public class DamageComponent : ComponentDataWrapper<Damage> { }

	// Apply damage in radius by slowing down speed of all rotators
	[UpdateAfter("PreLateUpdate.ParticleSystemBeginUpdateAll")]
	public class DamageSystem : JobComponentSystem
	{
		// Damage components and transforms are in first tuple set (both arrays are in sync and required)
		[InjectTuples(0)]
		public ComponentDataArray<Damage>   		m_Damages;
		[InjectTuples(0)]
		public TransformAccessArray  				m_DamageTransforms;

		// RotationSpeed components and transforms are in second tuple set (both arrays are in sync and required)
		[InjectTuples(1)]
		public ComponentDataArray<RotationSpeed>    m_RotationSpeed;
		[InjectTuples(1)]
		public TransformAccessArray  				m_RotationSpeedTransforms;

		public override void OnUpdate()
		{
			base.OnUpdate ();

			// Extract positions for both damage and rotations in two arrays
			// so that our N * N loop isn't doing complex calls and has tight data
			NativeArray<float3> damagePositions = new NativeArray<float3> (m_DamageTransforms.Length, Allocator.TempJob);
			NativeArray<float3> rotationPositions = new NativeArray<float3> (m_RotationSpeedTransforms.Length, Allocator.TempJob);
			var damagePositionsJob = GetPositionsJob.Schedule (m_DamageTransforms, damagePositions);
			var rotationsJob = GetPositionsJob.Schedule (m_RotationSpeedTransforms, rotationPositions);

			// Schedule job that applies damage (Slowing rotation speed down)
			// when the damage component is near the rotation component
			var damageJob = new ApplyDamage ();
			damageJob.deltaTime = Time.deltaTime;
			damageJob.damagePositions = damagePositions;
			damageJob.rotationPositions = rotationPositions;
			damageJob.rotationSpeeds = m_RotationSpeed;
			damageJob.damages = m_Damages;

			var dependency = JobHandle.CombineDependencies (damagePositionsJob, rotationsJob, GetDependency());
			AddDependency(damageJob.Schedule (m_RotationSpeed.Length, 64, dependency));
		}

		// Used to Extract positions from transforms
		struct GetPositionsJob : IJobParallelForTransform
		{
			public static JobHandle Schedule(TransformAccessArray transforms, NativeArray<float3> pos)
			{
				GetPositionsJob job;
				job.positions = pos;
				return job.Schedule (transforms);
			}
				
			NativeArray<float3> positions;

			public void Execute(int i, TransformAccess transform)
			{
				positions[i] = transform.position;
			}
		}

		// For each damage component checks if we are in the range, if so reduce speed of rotation speeds accordingly
		struct ApplyDamage : IJobParallelFor
		{
			public float 								deltaTime;

			[DeallocateOnJobCompletion]
			[ReadOnly]
			public NativeArray<float3> 					damagePositions;

			[DeallocateOnJobCompletion]
			[ReadOnly]
			public NativeArray<float3> 					rotationPositions;

			public ComponentDataArray<RotationSpeed> 	rotationSpeeds;

			[ReadOnly]
			public ComponentDataArray<Damage> 			damages;

			public void Execute(int r)
			{
				for (int d = 0; d != damages.Length; d++)
				{
					if (math.distance (damagePositions[d], rotationPositions[r]) < damages[d].radius)
					{
						var speed = rotationSpeeds[r];
						speed.speed = math.max (0.0F, speed.speed - (deltaTime * damages [d].slowdownPerSecond));
						rotationSpeeds[r] = speed;

	                    // Ref returns would be nice...
	                    // rotationSpeeds[r].speed = math.max (0.0F, rotationSpeeds[r].speed - (deltaTime * damages[d].slowdownPerSecond));
					}
				}
			}
		}
	}
}