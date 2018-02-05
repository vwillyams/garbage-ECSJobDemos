using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using UnityEngine.Rendering;

namespace UnityEngine.ECS.MeshInstancedShim
{
    [Serializable]
	public struct MeshInstancedShim : ISharedComponentData
	{
        public Mesh                 mesh;
        public Material             material;

        public ShadowCastingMode    castShadows;
        public bool                 receiveShadows;
	}

	public class MeshInstancedShimComponent : SharedComponentDataWrapper<MeshInstancedShim> { }
}
