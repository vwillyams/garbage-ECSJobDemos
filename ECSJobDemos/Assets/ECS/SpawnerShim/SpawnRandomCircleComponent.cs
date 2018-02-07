﻿using System;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.Rendering;

namespace UnityEngine.ECS.SpawnerShim
{
    [Serializable]
    public struct SpawnRandomCircle : ISharedComponentData
    {
        public GameObject prefab;
        public bool spawnLocal;
        public float radius;
        public int count;
    }

    public class SpawnRandomCircleComponent : SharedComponentDataWrapper<SpawnRandomCircle> { }
}
