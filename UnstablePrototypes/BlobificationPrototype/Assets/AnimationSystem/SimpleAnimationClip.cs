﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEditor;
using AnimationSampleCode;

class SimpleAnimationClip : ScriptableObject
{
	//@TODO: Need serialization & sensible lifecycle
	//       to do Dispose at a time after which the data will no longer be serialized...
	public BlobRootPtr<DenseClip> 	m_ClipData;
	public AnimationBinding			m_BindingData;

	public static SimpleAnimationClip FromAnimationClip(AnimationClip clip)
	{
		var simpleClip = ScriptableObject.CreateInstance<SimpleAnimationClip> ();
		AnimationSystem.AnimationClipToDenseClip(clip, out simpleClip.m_ClipData, out simpleClip.m_BindingData);
		return simpleClip;
	}

	public void CreateSkeletonBinding(Skeleton skeleton, out AnimationToSkeletonBinding binding)
	{
		AnimationSystem.AnimationClipToBinding (m_BindingData, skeleton, out binding);
	}

	void OnDisable()
	{
		m_ClipData.Dispose();		
		m_BindingData.Dispose();		
	}
}