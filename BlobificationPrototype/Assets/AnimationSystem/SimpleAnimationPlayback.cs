using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEditor;
using AnimationSampleCode;
using UnityEngine.Collections;

class SimpleAnimationPlayback : MonoBehaviour
{
	public AnimationClip m_SourceClip = null;

	SimpleAnimationClip 		m_GeneratedClip;
	AnimationToSkeletonBinding	m_ClipToSkeletonBinding;

	Skeleton 					m_Skeleton;
	LocalPoseData				m_DefaultPose;
	TransformAccessArray		m_TransformAccessArray;

	JobHandle					m_Job;

	void OnEnable()
	{
		AnimationSystem.CreateSkeletonFromGameObjectHierarchy (transform, out m_Skeleton, out m_DefaultPose, out m_TransformAccessArray);

		m_GeneratedClip = SimpleAnimationClip.FromAnimationClip (m_SourceClip);
		m_GeneratedClip.CreateSkeletonBinding (m_Skeleton, out m_ClipToSkeletonBinding);
	}

	struct SampleAnimationClipJob : IJob
	{
		public BlobRootPtr<DenseClip> 		clip;
		public Skeleton 					skeleton;
		public LocalPoseData				defaultPose;

		public AnimationToSkeletonBinding 	animationToSkeleton;

		public LocalPoseData				outputPose;

		public float time;

		public unsafe void Execute()
		{
			NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;

			var sampledCurves = new NativeArray<float>(animationToSkeleton.animationCurveToSkeletonIndex.Length, Allocator.Temp);

			DenseClip* denseClip = (DenseClip*)clip.UnsafeReadOnlyPtr;
			AnimationSystem.SampleAnimation (ref *denseClip, time, sampledCurves);

			AnimationSystem.CopyPose (defaultPose, outputPose);
			AnimationSystem.ApplyToSkeleton (sampledCurves, animationToSkeleton, outputPose);

			sampledCurves.Dispose ();
			NativeLeakDetection.Mode = NativeLeakDetectionMode.Enabled;
		}
	}

	struct ApplySkeletonToTransformJob : IJobParallelForTransform
	{
		[DeallocateOnJobCompletion]
		public LocalPoseData poseData;

		public void Execute(int index, TransformAccess transform)
		{
			transform.localPosition = poseData.pose[index].t;
			transform.localRotation = poseData.pose[index].r;
			transform.localScale    = poseData.pose[index].s;
		}
	}

	void Update()
	{
		m_Job.Complete ();

		//@TODO: Temporary hack, because TempJob still uses the leak detection.
		// It should not... Instead it should auto delete after 1 frame...
		NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
		var poseData = new LocalPoseData (m_DefaultPose.pose.Length, Allocator.TempJob);
		NativeLeakDetection.Mode = NativeLeakDetectionMode.Enabled;

		var sampleClipJob = new SampleAnimationClipJob ();
		sampleClipJob.clip = m_GeneratedClip.m_ClipData;
		sampleClipJob.skeleton = m_Skeleton;
		sampleClipJob.defaultPose = m_DefaultPose;
		sampleClipJob.animationToSkeleton = m_ClipToSkeletonBinding;
		sampleClipJob.outputPose = poseData;
		sampleClipJob.time = Time.time;
		m_Job = sampleClipJob.Schedule ();

		var applySkeletonJob = new ApplySkeletonToTransformJob();
		applySkeletonJob.poseData = poseData;

		m_Job = applySkeletonJob.Schedule(m_TransformAccessArray, m_Job);
	}

	void OnDisable()
	{
		m_Job.Complete ();

		m_Skeleton.Dispose ();
		m_DefaultPose.Dispose ();
		m_TransformAccessArray.Dispose ();
		m_ClipToSkeletonBinding.Dispose ();
	}
}