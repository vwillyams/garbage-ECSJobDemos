using System;
using UnityEngine;
using Unity.Collections;
using UnityEngine.ECS;
using UnityEngine.Rendering;

namespace UnityEngine.ECS.MeshInstancedHybrid
{
    [Serializable]
	public struct MeshInstancedHybrid : ISharedComponentData
	{
        public Mesh                 mesh;
        public Material             material;

        public ShadowCastingMode    castShadows;
        public bool                 receiveShadows;
	}

	public class MeshInstancedHybridComponent : SharedComponentDataWrapper<MeshInstancedHybrid> { }
}
