using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using UnityEngine.Assertions;
using UnityEngine.ECS;

namespace RotatorSamples
{

	class RotatorManagerWithTransformInject : ScriptBehaviourManager
	{
		TransformAccessArray 	m_Transforms;
		NativeList<float> 		m_Speeds;
		JobHandle 				m_Job;

		protected override void OnCreateManager (int capacity)
		{
			base.OnCreateManager (capacity);

			m_Transforms = new TransformAccessArray (capacity);
			m_Speeds = new NativeList<float> (capacity, Allocator.Persistent);
		}

		protected override void OnDestroyManager()
		{
			base.OnDestroyManager ();

			Assert.AreEqual(0, m_Speeds.Length);
			m_Transforms.Dispose();
			m_Speeds.Dispose();		
		}

		public override void OnUpdate()
		{
			base.OnUpdate ();

			m_Job.Complete ();

			var jobData = new RotatorJob();
			jobData.speeds = m_Speeds;
			jobData.deltaTime = Time.deltaTime;

			m_Job = jobData.Schedule (m_Transforms);
		}

		public int Add(Transform transform, float speed)
		{
			m_Job.Complete();
			m_Speeds.Add(speed);
			m_Transforms.Add(transform);
			return m_Transforms.Length - 1;
		}

		public void SetSpeed(int index, float speed)
		{
			m_Job.Complete();
			m_Speeds[index] = speed;
		}

		public void Remove(RotatorWithManagerTransformInject rotator)
		{
			m_Job.Complete();
			var lastRotator = m_Transforms[m_Transforms.Length - 1].GetComponent<RotatorWithManagerTransformInject> ();
			lastRotator.m_Index = rotator.m_Index;

			m_Speeds.RemoveAtSwapBack (rotator.m_Index);
			m_Transforms.RemoveAtSwapBack (rotator.m_Index);

			rotator.m_Index = -1;
		}

		struct RotatorJob : IJobParallelForTransform
		{
			[ReadOnly]
			public NativeArray<float> 	speeds;

			public float 				deltaTime;

			public void Execute(int index, TransformAccess transform)
			{
				transform.rotation = transform.rotation * Quaternion.AngleAxis (speeds[index] * deltaTime, Vector3.up);
			}
		}
	}

	[DisallowMultipleComponent]
	public class RotatorWithManagerTransformInject : ScriptBehaviour
	{
		// Both static & instance value dependency injection works.
		// (m_Manager can be marked static, this would result in less memory consumption,
		// but no per instance control over the manager from the dependency manager)
		[InjectDependency]
		RotatorManagerWithTransformInject 	m_Manager;

		[InjectDependency]
		Transform 	m_Transform;

		[SerializeField]
		float 					m_Speed;

		internal int 			m_Index = -1;

		public float speed
		{
			get { return m_Speed; }
			set
			{
				m_Speed = value;
				if (m_Index != -1)
					m_Manager.SetSpeed(m_Index, value);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable ();
			m_Index = m_Manager.Add(m_Transform, m_Speed);
		}

		protected override void OnDisable()
		{
			base.OnDisable ();
			m_Manager.Remove(this);
		}
	}
}