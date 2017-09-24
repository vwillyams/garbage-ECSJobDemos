using System;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.ECS;

namespace BoidSimulations
{
    [System.Serializable]
	public struct BoidInstanceRenderer : ISharedComponentData
	{
        public Material material;
        public Mesh     mesh;
	}

	public class BoidInstanceRendererComponent : SharedComponentDataWrapper<BoidInstanceRenderer> { }
}