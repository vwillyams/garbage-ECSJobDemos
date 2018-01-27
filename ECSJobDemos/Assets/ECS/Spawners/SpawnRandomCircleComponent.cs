using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.Rendering;

namespace UnityEngine.ECS.Spawners
{
    [Serializable]
    public struct SpawnRandomCircle : ISharedComponentData
    {
        public GameObject prefab;
        public float radius;
        public int count;
    }

    public class SpawnRandomCircleComponent : SharedComponentDataWrapper<SpawnRandomCircle> { }
}
