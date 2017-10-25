using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public class NativeQueuePerf : MonoBehaviour
{

	NativeQueue<int> m_queue;
	// Use this for initialization
	void Start ()
	{
		m_queue = new NativeQueue<int>(1024*1024, Allocator.Persistent);
	}

	void OnDisable()
	{
		m_queue.Dispose();
	}
	
	struct NativeQueueEnqueue : IJobParallelFor
	{
		public NativeQueue<int>.Concurrent m_queue;
		public void Execute(int index)
		{
			m_queue.Enqueue(index);			
		}
	}
	// Update is called once per frame
	void Update ()
	{
		m_queue.Clear();
		UnityEngine.Profiling.Profiler.BeginSample("QueueST");
		for (int i = 0; i < m_queue.Capacity / 2; ++i)
			m_queue.Enqueue(i);
		UnityEngine.Profiling.Profiler.EndSample();
		m_queue.Clear();
		UnityEngine.Profiling.Profiler.BeginSample("QueueST.Concurrent");
		NativeQueue<int>.Concurrent cq = m_queue;
		for (int i = 0; i < cq.Capacity / 2; ++i)
			cq.Enqueue(i);
		UnityEngine.Profiling.Profiler.EndSample();
		m_queue.Clear();
		var qjob = new NativeQueueEnqueue();
		qjob.m_queue = m_queue;
		qjob.Schedule(m_queue.Capacity / 2, 16).Complete();
	}
}
