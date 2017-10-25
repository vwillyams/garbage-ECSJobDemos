using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using Random = UnityEngine.Random;


[ComputeJobOptimization]
struct SphereCullingDemoJob: IJobParallelForFilter
{
	public static bool IntersectWithOr(float4 sphere, NativeArray<float4> planes)
	{
		bool result = false;
		for (int i = 0; i != planes.Length; i++)
		{
			float dist = math.dot(sphere.xyz, planes[i].xyz) + planes[i].w;
			result |= dist < -sphere.w;
		}
		return !result;
	}

	public NativeArray<float4> spheres;

	[ReadOnly]
	[NativeFixedLength(6)]
	public NativeArray<float4> cullingPlanes;

    public bool Execute(int index)
    {
		return IntersectWithOr (spheres[index], cullingPlanes);
    }
}

struct IndexListToBitSet : IJob
{
    public NativeList<int> visibleList;
    public NativeArray<bool> visibleBits;

    public void Execute()
    {
        for (int i = 0;i != visibleBits.Length;i++)
        	visibleBits[i] = false;
        for (int i = 0;i != visibleList.Length;i++)
        	visibleBits[visibleList[i]] = true;
    }
}

struct MoveTransforms : IJobParallelForTransform
{
    [ReadOnly]
    public NativeArray<bool> visibleBits;
    public float deltaTime;

    public void Execute(int index, TransformAccess transform)
    {
    	if (visibleBits[index])
    		transform.rotation *= Quaternion.Euler(0, deltaTime * 270, 0);
    }
}

public class SphereCullingDemo : MonoBehaviour
{
    public bool                    animateMaterial;
    public int                     instanceCount;
    public Vector3                 randomPositionSize = new Vector3(100, 0, 100);
	public Renderer                prefab;
	public Material[]              materialAnimation;

    Renderer[]                     m_Renderers;
    Transform[]                    m_Transforms;
    NativeArray<float4>    			m_BoundingSpheres;
    NativeArray<float4>             m_CullingPlanes;
    NativeList<int>                m_Visible;
    NativeArray<bool>              m_VisibleBits;
    TransformAccessArray           m_TransformAccesses;
    private JobHandle              m_JobFence;
	void OnEnable()
	{
	    m_VisibleBits = new NativeArray<bool>(instanceCount, Allocator.Persistent);
		m_Visible = new NativeList<int>(instanceCount, Allocator.Persistent);
		m_BoundingSpheres = new NativeArray<float4>(instanceCount, Allocator.Persistent);
	    m_CullingPlanes = new NativeArray<float4>(6, Allocator.Persistent);
	    m_Renderers = new Renderer[instanceCount];
	    m_Transforms = new Transform[instanceCount];

	    var root = new GameObject("Root");
	    for (int i = 0; i != instanceCount; i++)
		{
			m_Renderers[i] = (Renderer)Instantiate(prefab, Vector3.Scale(Random.insideUnitSphere, randomPositionSize), Random.rotation);
		    m_Renderers[i].enabled = true;
//		    if (animateMaterial)
  //  			m_Renderers[i].sharedMaterial = materialAnimation[Random.Range(0, materialAnimation.Length)];
		    m_Transforms[i] = m_Renderers[i].transform;


		    m_Transforms[i].parent = root.transform;

		    var bounds = m_Renderers[i].bounds;
			m_BoundingSpheres[i] = new float4(bounds.center, bounds.extents.magnitude);
		}

		m_TransformAccesses = new TransformAccessArray(m_Transforms);
	}

	void OnDisable()
	{
	    m_JobFence.Complete();
	    m_BoundingSpheres.Dispose();
	    m_VisibleBits.Dispose();
	    m_Visible.Dispose();
	    m_CullingPlanes.Dispose();
	    m_TransformAccesses.Dispose();
	}

    void Update()
	{
	    m_JobFence.Complete();
		var planes = GeometryUtility.CalculateFrustumPlanes (Camera.main);
		for (int i = 0; i != planes.Length; i++)
			m_CullingPlanes [i] = new float4 (planes[i].normal, planes[i].distance);

	    var cullingJob = new SphereCullingDemoJob();
		cullingJob.spheres = m_BoundingSpheres;
	    cullingJob.cullingPlanes = m_CullingPlanes;

		m_Visible.Clear ();
		var cullingJobFence = cullingJob.ScheduleAppend(m_Visible, cullingJob.spheres.Length, 512);

		if (animateMaterial)
		{
			cullingJobFence.Complete();
		}
		else
		{
			var indexToBitsJob = new IndexListToBitSet();
			indexToBitsJob.visibleList = m_Visible;
			indexToBitsJob.visibleBits = m_VisibleBits;
			var indexToBitsJobFence = indexToBitsJob.Schedule(cullingJobFence);
			
			var moveTransformsJob = new MoveTransforms();
			moveTransformsJob.visibleBits = indexToBitsJob.visibleBits;
			moveTransformsJob.deltaTime = Time.deltaTime;
		    m_JobFence = moveTransformsJob.Schedule(m_TransformAccesses, indexToBitsJobFence);
		}
	}
}
