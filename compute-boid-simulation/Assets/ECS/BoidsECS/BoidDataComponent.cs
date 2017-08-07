using System;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.ECS;

namespace BoidSimulations
{
	public struct BoidData : IComponentData
	{
		public float3  position;
		public float3  forward;

		public BoidData(float3 p, float3 f)
		{
			position = p;
			forward = f;
		}
	}

	public class BoidDataComponent : ComponentDataWrapper<BoidData>
	{
		//@TODO: This should really be possible from anywhere?
		static int kInstanceCycleOffsetProp = Shader.PropertyToID ("_InstanceCycleOffset");

		protected override void OnEnable()
		{
			base.OnEnable ();

			// init position from transform
			BoidData val;
			val.position = transform.position;
			val.forward = transform.forward;
			Value = val;

			// material randomization
			var block = new MaterialPropertyBlock ();
			block.SetFloat(kInstanceCycleOffsetProp, UnityEngine.Random.Range(0.0F, 2.0F));
			var meshRenderer = GetComponentInChildren<MeshRenderer> ();
			if (meshRenderer)
				meshRenderer.SetPropertyBlock (block);
		}
	}
}