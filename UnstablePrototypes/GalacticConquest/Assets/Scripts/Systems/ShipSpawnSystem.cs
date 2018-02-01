using System;
using System.Collections.Generic;
using Data;
using Other;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Systems
{
    [UpdateAfter(typeof(UserInputSystem))]
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

        protected override void OnCreateManager(int capacity)
        {
            _shipsToSpawn = new NativeList<ShipSpawnData>(Allocator.Persistent);
        }

        protected override void OnDestroyManager()
        {
            _shipsToSpawn.Dispose();
        }

        [Inject] private SpawningPlanets _planets;
        private PrefabManager _prefabManager;
        private EntityManager _entityManager;

        NativeList<ShipSpawnData> _shipsToSpawn;

        protected override void OnUpdate()
        {

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

                var dt = Time.deltaTime;
                var deltaSpawn = Math.Max(1, Convert.ToInt32(1000.0f * dt));

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

                    _shipsToSpawn.Add(new ShipSpawnData
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
            var entities = new NativeArray<Entity>(_shipsToSpawn.Length, Allocator.Temp);
            _entityManager.Instantiate(_prefabManager.ShipPrefab, entities);
            for (int i = 0; i < _shipsToSpawn.Length; i++)
            {
                var data = new ShipData
                {
                    TargetEntity = _shipsToSpawn[i].TargetEntity,
                    TeamOwnership = _shipsToSpawn[i].TeamOwnership
                };
                _entityManager.AddComponent(entities[i], data);
                var spawnPosition = new TransformPosition
                {
                    position = _shipsToSpawn[i].SpawnPosition
                };

                var transformMatix = new TransformMatrix
                {
                    matrix = math.scale(new float3(0.02f, 0.02f, 0.02f))
                };

                _entityManager.SetComponent(entities[i], transformMatix);
                _entityManager.SetComponent(entities[i], spawnPosition);

            }
            entities.Dispose();
            _shipsToSpawn.Clear();
        }
    }
}
