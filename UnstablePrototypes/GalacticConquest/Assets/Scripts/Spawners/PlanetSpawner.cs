﻿using System.Collections.Generic;
using System.Linq;
using Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlanetSpawner : MonoBehaviour
{
    [SerializeField]
    GameObject _planetPrefab;
    [SerializeField]
    int _initialCount = 20;
    [SerializeField] readonly float radius = 100.0f;
    EntityManager _entityManager;
    [SerializeField]
    Material[] _teamMaterials;
    static Dictionary<Entity, GameObject> entities = new Dictionary<Entity, GameObject>();

    public static Material[] TeamMaterials;

    void Instantiate(int count)
    {
        var planetOwnership = new List<int>
        {
            1, 1,
            2, 2
        };

        for (var i = 0; i < count; ++i)
        {


            var sphereRadius = Random.Range(5.0f, 20.0f);
            var safe = false;
            float3 pos;
            int attempts = 0;
            do
            {
                if (++attempts >= 500)
                {
                    Debug.Log("Couldn't find a good planet placement. Settling for the planets we already have");
                    return;
                }
                var randomValue = (Vector3) Random.insideUnitSphere;
                randomValue.y = 0;
                pos = (randomValue * radius) + new Vector3(transform.position.x, transform.position.z);
                var collisions = Physics.OverlapSphere(pos, sphereRadius);
                if (!collisions.Any())
                    safe = true;
            } while (!safe);
            var randomRotation = Random.insideUnitSphere;
            var go = GameObject.Instantiate(_planetPrefab, pos, Quaternion.identity);
            var planetEntity = go.GetComponent<GameObjectEntity>().Entity;
            var meshGo = go.GetComponentsInChildren<Transform>().First(c => c.gameObject != go).gameObject;
            var collider = go.GetComponent<SphereCollider>();
            var meshEntity = meshGo.GetComponent<GameObjectEntity>().Entity;

            collider.radius = sphereRadius;
            meshGo.transform.localScale = new Vector3(sphereRadius * 2.0f, sphereRadius * 2.0f, sphereRadius * 2.0f);

            var planetData = new PlanetData
            {
                TeamOwnership = 0,
                Radius = sphereRadius,
                Position = pos
            };
            var rotationData = new RotationData
            {
                RotationSpeed = randomRotation
            };
            if (planetOwnership.Any())
            {
                planetData.TeamOwnership = planetOwnership.First();
                planetOwnership.Remove(planetData.TeamOwnership);
            }
            else
            {
                planetData.Occupants = Random.Range(1, 100);
            }
            entities[planetEntity] = go;
            SetColor(planetEntity, planetData.TeamOwnership);

            _entityManager.AddComponentData(planetEntity, planetData);
            _entityManager.AddComponentData(meshEntity, rotationData);
        }
    }

    void OnEnable()
    {
        TeamMaterials = _teamMaterials;
        _entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Instantiate(_initialCount);
    }

    public static void SetColor(Entity entity, int team)
    {
        var go = entities[entity];
        go.GetComponentsInChildren<MeshRenderer>().First(c => c.gameObject != go).material = TeamMaterials[team];
    }
}
