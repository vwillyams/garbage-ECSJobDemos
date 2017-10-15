using System;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.ECS;

namespace UnityEngine.ECS.Rendering
{
    [System.Serializable]
	public struct InstanceRenderer : ISharedComponentData
	{
        public Material material;
        public Mesh     mesh;
	}

	public class InstanceRendererComponent : SharedComponentDataWrapper<InstanceRenderer> { }
}