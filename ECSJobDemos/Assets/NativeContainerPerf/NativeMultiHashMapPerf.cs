using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public class NativeMultiHashMapPerf : MonoBehaviour
{

	NativeMultiHashMap<int, int> m_hashMap;
	// Use this for initialization
	void Start ()
	{
		m_hashMap = new NativeMultiHashMap<int, int>(1024*1024, Allocator.Persistent);
	}

	void OnDisable()
	{
		m_hashMap.Dispose();
	}
	
	struct NativeMultiHashMapAdd : IJobParallelFor
	{
		public NativeMultiHashMap<int, int>.Concurrent m_hashMap;
		public void Execute(int index)
		{
			m_hashMap.Add(index, index*index);			
		}
	}
	// Update is called once per frame
	void Update ()
	{
		m_hashMap.Clear();
		UnityEngine.Profiling.Profiler.BeginSample("MultiHashMapST");
		for (int i = 0; i < m_hashMap.Capacity / 2; ++i)
			m_hashMap.Add(i, i*2);
		UnityEngine.Profiling.Profiler.EndSample();
		m_hashMap.Clear();
		UnityEngine.Profiling.Profiler.BeginSample("MultiHashMapST.Concurrent");
		NativeMultiHashMap<int, int>.Concurrent chm = m_hashMap;
		for (int i = 0; i < chm.Capacity / 2; ++i)
			chm.Add(i, i*i);
		UnityEngine.Profiling.Profiler.EndSample();
		m_hashMap.Clear();
		var qjob = new NativeMultiHashMapAdd();
		qjob.m_hashMap = m_hashMap;
		qjob.Schedule(m_hashMap.Capacity / 2, 16).Complete();
	}
}
