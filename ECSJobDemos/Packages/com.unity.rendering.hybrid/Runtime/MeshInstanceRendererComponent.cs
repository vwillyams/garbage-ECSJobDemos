﻿using System;
using Unity.ECS;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Rendering.Hybrid
{
    /// <summary>
    /// Render Mesh wiih Material (must be instanced material) by object to world matrix
    /// specified by TransformMatrix associated with entity.
    /// </summary>
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
