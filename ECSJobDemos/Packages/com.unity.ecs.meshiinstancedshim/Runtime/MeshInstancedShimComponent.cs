using System;
using Unity.ECS;
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
