using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEditor;
using UnityEngine.Assertions;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace AnimationSampleCode
{
	[StructLayout (LayoutKind.Sequential)]
	struct trsX
	{
		public float3 				t;
		public float4     			r;
		public float3				s;

		public trsX (float3 t, float4 r, float3 s)
		{
			this.t = t;
			this.r = r;
			this.s = s;
		}
	}

	struct Skeleton
	{
		public NativeArray<int> 			parents;
		public NativeArray<PropertyName> 	nodeName;

		public void Dispose()
		{
			parents.Dispose ();
			nodeName.Dispose ();
		}
	}

	struct LocalPoseData
	{
		public NativeArray<trsX> 	pose;

		public LocalPoseData(int size, Allocator allocator)
		{
			pose = new NativeArray<trsX> (size, allocator);
		}

		public void Dispose()
		{
			pose.Dispose ();
		}
	}

	struct AnimationBinding
	{
		public struct Data
		{
			public PropertyName 	transformPath;
			public int 		 		type;
		}

		public NativeArray<Data> 	bindings;

		public void Dispose()
		{
			bindings.Dispose ();
		}
	}

	struct DenseClip
	{
		public BlobArray<float> 				samples;
		public int 								frameCount;
		public int								curveCount;
		public float							sampleRate;
	}

	struct AnimationToSkeletonBinding
	{
		public NativeArray<int> animationCurveToSkeletonIndex;

		public void Dispose()
		{
			animationCurveToSkeletonIndex.Dispose ();
		}
	}

	class AnimationSystem
	{

		public static void ApplyToSkeleton(NativeArray<float> samples, AnimationToSkeletonBinding binding, LocalPoseData outputPose)
		{
			var outputTRSFloatBuffer = outputPose.pose.Slice().SliceConvert<float> ();
			for (int i = 0; i != samples.Length;i++)
			{
				int skeletonIndex = binding.animationCurveToSkeletonIndex[i];
				if (skeletonIndex != -1)
				{
					float sample = samples[i];
					outputTRSFloatBuffer[skeletonIndex] = sample;
				}
			}
		}

		public static void SampleAnimation(ref DenseClip clip, float time, NativeArray<float> samples)
		{
			float sampleIndex = Mathf.Repeat(time * clip.sampleRate, clip.frameCount);

			int lhsIndex = Mathf.FloorToInt(sampleIndex) * clip.curveCount;
			int rhsIndex = Mathf.Min(Mathf.CeilToInt(sampleIndex), clip.frameCount-1) * clip.curveCount;
			float w = sampleIndex - Mathf.FloorToInt(sampleIndex);

			for (int i = 0; i < clip.curveCount; i++)
			{
				float lhs = clip.samples [i + lhsIndex];
				float rhs = clip.samples [i + rhsIndex];
				samples[i] = Mathf.Lerp (lhs, rhs, w);
			}
		}

		public static void CopyPose(LocalPoseData inPose, LocalPoseData outPose)
		{
			//@TODO: not convenient. Need some copy array + slicing API's
			for (int i = 0; i != inPose.pose.Length; i++)
				outPose.pose[i] = inPose.pose[i];
		}

		public unsafe static void AnimationClipToBinding(AnimationBinding animationBinding, Skeleton skeleton, out AnimationToSkeletonBinding outBindings)
		{
			outBindings.animationCurveToSkeletonIndex = new NativeArray<int> (animationBinding.bindings.Length, Allocator.Persistent);

			Assert.AreEqual (sizeof(float) * kCurvePerTRSX, Marshal.SizeOf(typeof(trsX)));

			for (int i = 0; i != animationBinding.bindings.Length; i++)
			{
				var binding = animationBinding.bindings[i];

				outBindings.animationCurveToSkeletonIndex[i] = -1;

				//@TODO: Need better NativeArray perf...
				PropertyName* skeletonNodeNames = (PropertyName*)skeleton.nodeName.GetUnsafeReadOnlyPtr();
				int skeletonNodeNamesLength = skeleton.nodeName.Length;
				for (int s = 0; s != skeletonNodeNamesLength; s++)
				{
					if (skeletonNodeNames[s] == binding.transformPath)
						outBindings.animationCurveToSkeletonIndex[i] = s * kCurvePerTRSX + binding.type;
				}

				if (outBindings.animationCurveToSkeletonIndex [i] == -1)
					Debug.LogWarning ("Binding failed: " + animationBinding.bindings[i].transformPath);
			}
		}

		const int kCurvePerTRSX = 10;

		static int BindPropertyNameToTRSOffset(string propertyName)
		{
			switch (propertyName)
			{
			case "m_LocalPosition.x":
				return 0;
			case "m_LocalPosition.y":
				return 1;
			case "m_LocalPosition.z":
				return 2;

			case "m_LocalRotation.x":
				return 3;
			case "m_LocalRotation.y":
				return 4;
			case "m_LocalRotation.z":
				return 5;
			case "m_LocalRotation.w":
				return 6;

			case "m_LocalScale.x":
				return 7;
			case "m_LocalScale.y":
				return 8;
			case "m_LocalScale.z":
				return 9;

			default:
				throw new System.ArgumentException("not supported binding: " + propertyName);
			}
		}

		public unsafe static void AnimationClipToDenseClip(AnimationClip clip, out BlobRootPtr<DenseClip> outClip, out AnimationBinding outBindings)
		{
			var bindings = AnimationUtility.GetCurveBindings (clip);

			outBindings.bindings = new NativeArray<AnimationBinding.Data> (bindings.Length, Allocator.Persistent);

			int frameCount = Mathf.CeilToInt (clip.frameRate * clip.length);

			var blobAllocator = new BlobAllocator (Allocator.Persistent, frameCount * bindings.Length * sizeof(float) + sizeof(DenseClip));

			var clipData = (DenseClip*)blobAllocator.ConstructRoot<DenseClip> ();
			clipData->curveCount = bindings.Length;
			clipData->frameCount = frameCount;
			clipData->sampleRate = clip.frameRate;

			blobAllocator.Allocate(clipData->curveCount * clipData->frameCount, ref clipData->samples);

			int outputIndex = 0;
			foreach (var binding in bindings)
			{
				var outBinding = new AnimationBinding.Data();
				outBinding.transformPath = binding.path;
				outBinding.type = BindPropertyNameToTRSOffset (binding.propertyName);
				outBindings.bindings[outputIndex] = outBinding;

				var curve = AnimationUtility.GetEditorCurve (clip, binding);
				for (int f = 0; f != clipData->frameCount;f++)
					clipData->samples[outputIndex + f * clipData->curveCount] = curve.Evaluate(f / clipData->sampleRate);

				outputIndex++;
			}

			outClip = blobAllocator.Create<DenseClip> ();
		}

		struct SkeletonBuilder
		{
			public NativeList<int> 			parents;
			public NativeList<PropertyName> nodeName;
			public NativeList<trsX>			defaultPose;
			public List<Transform>			transforms;

			public SkeletonBuilder(int capacity)
			{
				parents = new NativeList<int> (Allocator.Persistent);
				nodeName = new NativeList<PropertyName> (Allocator.Persistent);
				defaultPose = new NativeList<trsX> (Allocator.Persistent);
				transforms = new List<Transform>();
			}

			public void Dispose ()
			{
				parents.Dispose ();
				nodeName.Dispose ();
				defaultPose.Dispose ();
			}
		}

		static void BuildSkeletonRecursive(Transform root, Transform transform, int parentIndex, ref SkeletonBuilder builder)
		{
			int index = builder.transforms.Count;
			builder.parents.Add (parentIndex);
			builder.nodeName.Add (UnityEditor.AnimationUtility.CalculateTransformPath(transform, root));
			builder.defaultPose.Add (new trsX(transform.localPosition, transform.localRotation, transform.localScale));
			builder.transforms.Add (transform);
			foreach (Transform child in transform)
				BuildSkeletonRecursive (root, child, index, ref builder);
		}

		public static void CreateSkeletonFromGameObjectHierarchy(Transform root, out Skeleton skeleton, out LocalPoseData defaultPose, out TransformAccessArray transforms)
		{
			var builder = new SkeletonBuilder (100);
			BuildSkeletonRecursive (root, root, -1, ref builder);

			skeleton = new Skeleton ();
			defaultPose = new LocalPoseData ();

			skeleton.parents = new NativeArray<int> (builder.parents, Allocator.Persistent);
			skeleton.nodeName = new NativeArray<PropertyName> (builder.nodeName, Allocator.Persistent);

			defaultPose.pose = new NativeArray<trsX> (builder.defaultPose, Allocator.Persistent);

			transforms = new TransformAccessArray (builder.transforms.ToArray());


			builder.Dispose ();
		}
	}
}
