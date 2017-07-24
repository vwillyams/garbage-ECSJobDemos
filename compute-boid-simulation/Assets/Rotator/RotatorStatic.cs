using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;

[DisallowMultipleComponent]
public class RotatorStatic : MonoBehaviour
{
	static TransformAccessArray 	ms_Transforms;
	static NativeList<float> 		ms_Speeds;
	static JobHandle				ms_Job;

	[SerializeField]
	float 							m_Speed;
	int 							m_Index = -1;

	public float speed
	{
		get { return m_Speed; }
		set
		{
			m_Speed = value;
			if (m_Index != -1)
			{
				ms_Job.Complete ();
				ms_Speeds [m_Index] = value;
			}
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Initialize()
	{
		ms_Transforms = new TransformAccessArray (0);
		ms_Speeds = new NativeList<float> (Allocator.Persistent);

		PlayerLoopManager.RegisterUpdate(Update, PlayerLoopManager.Phase.PreUpdate);
		PlayerLoopManager.RegisterDomainUnload (ShutDown);
	}

	static void ShutDown()
	{
		ms_Transforms.Dispose ();
		ms_Speeds.Dispose ();
	}

	void OnEnable()
	{
		ms_Job.Complete ();

		m_Index = ms_Transforms.Length;
		ms_Speeds.Add(m_Speed);
		ms_Transforms.Add(transform);
	}

	void OnDisable()
	{
		ms_Job.Complete ();

		ms_Transforms[ms_Transforms.Length - 1].GetComponent<RotatorStatic>().m_Index = m_Index;
		ms_Speeds.RemoveAtSwapBack (m_Index);
		ms_Transforms.RemoveAtSwapBack (m_Index);

		m_Index = -1;
	}

	static void Update ()
	{
		ms_Job.Complete ();

		var job = new RotatorJob();
		job.speeds = ms_Speeds;
		job.deltaTime = Time.deltaTime;

		ms_Job = job.Schedule (ms_Transforms);
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
