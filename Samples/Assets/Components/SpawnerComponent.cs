using System;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct Spawner : ISharedComponentData
{
	public GameObject prefab;
	public bool spawnLocal;
	public float radius;
	public int count;
}

public class SpawnerComponent : SharedComponentDataWrapper<Spawner> { }