using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Assertions;
using UnityEngine.ECS;

namespace RotatorSamples
{

	class RotatorManagerMainThread : ScriptBehaviourManager
	{
		List<Transform>			m_Transforms;
		NativeList<float> 		m_Speeds;

		protected override void OnCreateManager (int capacity)
		{
			base.OnCreateManager (capacity);

			m_Transforms = new List<Transform> (capacity);
			m_Speeds = new NativeList<float> (capacity, Allocator.Persistent);
		}

		protected override void OnDestroyManager()
		{
			base.OnDestroyManager ();

			Assert.AreEqual(0, m_Speeds.Length);
			m_Speeds.Dispose();		
		}

		public override void OnUpdate()
		{
			base.OnUpdate ();

			float deltaTime = Time.deltaTime;
			NativeArray<float> speeds = m_Speeds;
			for (int i = 0; i != m_Transforms.Count; i++)
			{
				var transform = m_Transforms [i];
				transform.rotation = transform.rotation * Quaternion.AngleAxis (speeds[i] * deltaTime, Vector3.up);
			}
		}

		public int Add(Transform transform, float speed)
		{
			m_Speeds.Add(speed);
			m_Transforms.Add(transform);
			return m_Transforms.Count - 1;
		}

		public void SetSpeed(int index, float speed)
		{
			m_Speeds[index] = speed;
		}

		public void Remove(RotatorWithManagerMainThread rotator)
		{
			var lastRotator = m_Transforms[m_Transforms.Count - 1].GetComponent<RotatorWithManagerMainThread> ();
			lastRotator.m_Index = rotator.m_Index;

			m_Speeds.RemoveAtSwapBack (rotator.m_Index);
			m_Transforms.RemoveAtSwapBack (rotator.m_Index);

			rotator.m_Index = -1;
		}
	}

	[DisallowMultipleComponent]
	public class RotatorWithManagerMainThread : ScriptBehaviour
	{
		// Both static & instance value dependency injection works.
		// (m_Manager can be marked static, this would result in less memory consumption,
		// but no per instance control over the manager from the dependency manager)
		[InjectDependency]
		RotatorManagerMainThread 	m_Manager;

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