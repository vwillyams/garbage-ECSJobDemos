using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.Rendering;

namespace UnityEngine.ECS.SpawnerShim
{
    [Serializable]
    public struct SpawnRandomInSphere : ISharedComponentData
    {
        public GameObject prefab;
        public float radius;
        public int count;
    }

    public class SpawnRandomInSphereComponent : SharedComponentDataWrapper<SpawnRandomInSphere> { }
}
