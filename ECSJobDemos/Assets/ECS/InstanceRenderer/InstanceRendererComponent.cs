using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using UnityEngine.Rendering;

namespace UnityEngine.ECS.Rendering
{
    [System.Serializable]
	public struct InstanceRenderer : ISharedComponentData
	{
        public Mesh                 mesh;
        public Material             material;

        public ShadowCastingMode    castShadows;
        public bool                 receiveShadows;
	}

	public class InstanceRendererComponent : SharedComponentDataWrapper<InstanceRenderer> { }
}