using System;
using System.Collections.Generic;
using Data;
using Other;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Systems
{
    [UpdateAfter(typeof(UserActionSystem))]
    public class ShipSpawnSystem : ComponentSystem
    {
        public ShipSpawnSystem()
        {
            _prefabManager = GameObject.FindObjectOfType<PrefabManager>();
            _entityManager = World.Active.GetOrCreateManager<EntityManager>();
        }
        struct SpawningPlanets
        {
            public int Length;
            public ComponentDataArray<PlanetShipLaunchData> Data;
        }

        struct ShipSpawnData
        {
            public Vector3 SpawnPosition;
            public Entity TargetEntity;
            public int TeamOwnership { get; set; }
        }

        [InjectComponentGroup] private SpawningPlanets _planets;
        private PrefabManager _prefabManager;
        private EntityManager _entityManager;

        private float spawnCounter = 0.0f;

        protected override void OnUpdate()
        {
            var shipData = new NativeList<ShipSpawnData>(Allocator.Temp);
            for(var planetIndex = 0; planetIndex < _planets.Length; planetIndex++)
            {
                var planetLaunchData = _planets.Data[planetIndex];
                if (planetLaunchData.NumberToSpawn == 0)
                {
                    continue;
                }
                var planetPos = new Vector3(planetLaunchData.SpawnLocation.x, planetLaunchData.SpawnLocation.y, planetLaunchData.SpawnLocation.z);

                var planetRadius = planetLaunchData.SpawnRadius;
                int shipCount;
                var shipsToSpawn = planetLaunchData.NumberToSpawn;

                spawnCounter += Time.deltaTime;
                var deltaSpawn = Math.Max(1, Convert.ToInt32(500.0f * spawnCounter));
                if (deltaSpawn >= 1)
                    spawnCounter = 0;
                if (deltaSpawn < shipsToSpawn)
                    shipsToSpawn = deltaSpawn;
                var targetPlanet = _entityManager.GetComponent<PlanetData>(planetLaunchData.TargetEntity);
                var planetDistance = Vector3.Distance(planetPos, targetPlanet.Position);
                for (shipCount = 0; shipCount < shipsToSpawn; shipCount++)
                {

                    Vector3 shipPos;
                    do
                    {
                        var insideCircle = Random.insideUnitCircle.normalized;
                        var onSphere = new Vector3(insideCircle.x, 0, insideCircle.y);
                        shipPos = planetPos + (onSphere * (planetRadius + _prefabManager.ShipPrefab.transform.localScale.x));
                    } while (Vector3.Distance(shipPos, targetPlanet.Position) > planetDistance);

                    shipData.Add(new ShipSpawnData
                    {
                        SpawnPosition = shipPos,
                        TargetEntity = planetLaunchData.TargetEntity,
                        TeamOwnership = planetLaunchData.TeamOwnership
                    });
                }
                var launchData = new PlanetShipLaunchData
                {
                    TargetEntity = planetLaunchData.TargetEntity,
                    NumberToSpawn = planetLaunchData.NumberToSpawn - shipsToSpawn,
                    TeamOwnership = planetLaunchData.TeamOwnership,
                    SpawnLocation = planetLaunchData.SpawnLocation,
                    SpawnRadius = planetLaunchData.SpawnRadius
                };
                _planets.Data[planetIndex] = launchData;
            }
            for (int i = 0; i < shipData.Length; i++)
            {
                var go = Object.Instantiate<GameObject>(_prefabManager.ShipPrefab, shipData[i].SpawnPosition, Quaternion.identity);

                var data = new ShipData
                {
                    TargetEntity = shipData[i].TargetEntity,
                    TeamOwnership = shipData[i].TeamOwnership
                };
                _entityManager.AddComponent(go.GetComponent<GameObjectEntity>().Entity, data);
            }

            shipData.Dispose();
        }
    }
}
