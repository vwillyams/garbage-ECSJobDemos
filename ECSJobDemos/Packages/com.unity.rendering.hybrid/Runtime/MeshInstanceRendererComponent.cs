using System;
using Unity.ECS;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Rendering.Hybrid
{
    [Serializable]
	public struct MeshInstanceRenderer : ISharedComponentData
	{
        public Mesh                 mesh;
        public Material             material;

        public ShadowCastingMode    castShadows;
        public bool                 receiveShadows;
	}

	public class MeshInstanceRendererComponent : SharedComponentDataWrapper<MeshInstanceRenderer> { }
}
