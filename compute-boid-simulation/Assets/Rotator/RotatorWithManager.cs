using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Assertions;
using UnityEngine.ECS;
using UnityEngine.Jobs;

namespace RotatorSamples
{

	class RotatorManager : ScriptBehaviourManager
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

		public void Remove(RotatorWithManager rotator)
		{
			m_Job.Complete();
			var lastRotator = m_Transforms[m_Transforms.Length - 1].GetComponent<RotatorWithManager> ();
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
	public class RotatorWithManager : ScriptBehaviour
	{
		// Both static & instance value dependency injection works.
		// (m_Manager can be marked static, this would result in
		// + less memory consumption on each instance
		// - unclear how we can release the manager when the last instance is released?
		// - no per instance control over the manager from the dependency manager)
		[InjectDependency]
		RotatorManager 	m_Manager;

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
			m_Index = m_Manager.Add(transform, m_Speed);
		}

		protected override void OnDisable()
		{
			base.OnDisable ();
			m_Manager.Remove(this);
		}
	}
}