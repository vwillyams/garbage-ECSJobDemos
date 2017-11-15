using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public class NativeHashMapPerf : MonoBehaviour
{

	NativeHashMap<int, int> m_hashMap;
	// Use this for initialization
	void Start ()
	{
		m_hashMap = new NativeHashMap<int, int>(1024*1024, Allocator.Persistent);
	}

	void OnDisable()
	{
		m_hashMap.Dispose();
	}
	
	struct NativeHashMapAdd : IJobParallelFor
	{
		public NativeHashMap<int, int>.Concurrent m_hashMap;
		public void Execute(int index)
		{
			m_hashMap.TryAdd(index, index*index);			
		}
	}
	// Update is called once per frame
	void Update ()
	{
		m_hashMap.Clear();
		UnityEngine.Profiling.Profiler.BeginSample("HashMapST");
		for (int i = 0; i < m_hashMap.Capacity / 2; ++i)
			m_hashMap.TryAdd(i, i*2);
		UnityEngine.Profiling.Profiler.EndSample();
		m_hashMap.Clear();
		UnityEngine.Profiling.Profiler.BeginSample("HashMapST.Concurrent");
		NativeHashMap<int, int>.Concurrent chm = m_hashMap;
		for (int i = 0; i < chm.Capacity / 2; ++i)
			chm.TryAdd(i, i*i);
		UnityEngine.Profiling.Profiler.EndSample();
		m_hashMap.Clear();
		var qjob = new NativeHashMapAdd();
		qjob.m_hashMap = m_hashMap;
		qjob.Schedule(m_hashMap.Capacity / 2, 16).Complete();
	}
}
