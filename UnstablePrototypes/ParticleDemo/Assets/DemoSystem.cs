using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class DemoSystem : ComponentSystem
{
    struct ActiveEmitter
    {
        public Entity entity;
        public float timeToLive;
    }

    private NativeList<ActiveEmitter> activeEmitters;
    private GameObject explosionPrefab;
    private GameObject smokePrefab;

    override protected void OnCreateManager(int cap)
    {
        activeEmitters = new NativeList<ActiveEmitter>(1024, Allocator.Persistent);
        explosionPrefab = (GameObject)Resources.Load("explosion", typeof(GameObject));
        smokePrefab = (GameObject)Resources.Load("smoke", typeof(GameObject));
    }

    override protected void OnDestroyManager()
    {
        activeEmitters.Dispose();
    }

    override protected void OnUpdate()
    {
        for (int i = 0; i < activeEmitters.Length; ++i)
        {
            var em = activeEmitters[i];
            em.timeToLive -= Time.deltaTime;
            activeEmitters[i] = em;
            if (em.timeToLive < 0)
            {
                EntityManager.DestroyEntity(em.entity);
                activeEmitters.RemoveAtSwapBack(i);
                --i;
            }
        }

        while (activeEmitters.Length < 128)
        {
            var em = new ActiveEmitter();
            em.timeToLive = Random.Range(0.5f, 2.0f);
            em.entity = EntityManager.Instantiate(explosionPrefab);
            var pos = new PositionComponentData(Random.Range(0, Screen.width), Random.Range(0, Screen.height));
            EntityManager.AddComponentData(em.entity, pos);
            EntityManager.AddComponentData(em.entity, new RotationComponentData(0));
            activeEmitters.Add(em);

            var sem = new ActiveEmitter();
            sem.timeToLive = em.timeToLive;
            sem.entity = EntityManager.Instantiate(smokePrefab);
            EntityManager.AddComponentData(sem.entity, pos);
            EntityManager.AddComponentData(sem.entity, new RotationComponentData(0));
            activeEmitters.Add(sem);

        }
    }
}
