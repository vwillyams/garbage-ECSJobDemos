using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

public class NativeMultiHashMapPerf : MonoBehaviour
{
    const int elementCount = 1024*1024;
	NativeMultiHashMap<int, int> m_hashMap;
    NativeArray<int> m_values;
	// Use this for initialization
	void Start ()
	{
		m_hashMap = new NativeMultiHashMap<int, int>(elementCount*2, Allocator.Persistent);
	    m_values = new NativeArray<int>(elementCount, Allocator.Persistent);
	}

	void OnDisable()
	{
		m_hashMap.Dispose();
	    m_values.Dispose();
	}

    [ComputeJobOptimization]
	struct NativeMultiHashMapAdd : IJobParallelFor
	{
		public NativeMultiHashMap<int, int>.Concurrent m_hashMap;
		public void Execute(int index)
		{
			m_hashMap.Add(index & 0xffff, index*index);
		}
	}

    [ComputeJobOptimization]
    struct NativeMultiHashMap_TryGetFirstValue : IJobParallelFor
    {
        [ReadOnly] public NativeMultiHashMap<int, int> m_hashMap;
        public NativeArray<int> m_values;
        public void Execute(int index)
        {
            NativeMultiHashMapIterator<int> iter;
            int val;
            if (m_hashMap.TryGetFirstValue(index & 0xffff, out val, out iter))
            {
                m_values[index] = val;
            }
        }
    }

	// Update is called once per frame
	void Update ()
	{
		m_hashMap.Clear();
		Profiler.BeginSample("MultiHashMapST");
		for (int i = 0; i < elementCount; ++i)
			m_hashMap.Add(i, i*2);
		Profiler.EndSample();
		m_hashMap.Clear();
		Profiler.BeginSample("MultiHashMapST.Concurrent");
		NativeMultiHashMap<int, int>.Concurrent chm = m_hashMap;
		for (int i = 0; i < elementCount; ++i)
			chm.Add(i & 0xffff, i*i);
		Profiler.EndSample();
		m_hashMap.Clear();

	    var qjob = new NativeMultiHashMapAdd();
		qjob.m_hashMap = m_hashMap;
		qjob.Schedule(elementCount, 16).Complete();

	    var tryGetFirstValueJob = new NativeMultiHashMap_TryGetFirstValue();
	    tryGetFirstValueJob.m_hashMap = m_hashMap;
	    tryGetFirstValueJob.m_values = m_values;
	    tryGetFirstValueJob.Schedule(elementCount, 16).Complete();
	}
}
