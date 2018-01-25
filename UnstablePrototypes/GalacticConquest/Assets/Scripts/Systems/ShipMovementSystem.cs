using System;
using System.Collections.Generic;
using System.Diagnostics;
using Data;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Systems
{
    [UpdateAfter(typeof(ShipSpawnSystem))]
    public class ShipMovementSystem : ComponentSystem
    {
        public ShipMovementSystem()
        {
            _entityManager = World.Active.GetOrCreateManager<EntityManager>();
        }
        struct Ships
        {
            public int Length;
            public ComponentArray<Transform> Transforms;
            public ComponentDataArray<ShipData> Data;
        }

        struct Planets
        {
            public int Length;
            public ComponentArray<Transform> Transforms;
            public ComponentDataArray<PlanetData> Data;
        }

        [InjectComponentGroup] private Ships _ships;
        [InjectComponentGroup] private Planets _planets;
        private EntityManager _entityManager;

        protected override void OnUpdate()
        {
            var arrivingShipData = new NativeList<ShipData>(Allocator.Temp);
            var arrivingShipTransforms = new List<Transform>();
            for (var shipIndex = 0; shipIndex < _ships.Length; shipIndex++)
            {
                var shipData = _ships.Data[shipIndex];
                var shipTransform = _ships.Transforms[shipIndex];
                var targetPlanet = _entityManager.GetComponent<PlanetData>(shipData.TargetEntity);
                //var targetTransform = shipData.TargetTransform;

                var newPos = Vector3.MoveTowards(shipTransform.position, targetPlanet.Position, Time.deltaTime);
                bool3 huh;
                if (Vector3.Distance(targetPlanet.Position, newPos) <= targetPlanet.Radius)
                {
                    arrivingShipData.Add(shipData);
                    arrivingShipTransforms.Add(shipTransform);
                    continue;
                }
                for (var planetIndex = 0; planetIndex < _planets.Length; planetIndex++)
                {
                    var planet = _planets.Data[planetIndex];
                    if (Vector3.Distance(newPos, planet.Position) < planet.Radius)
                    {
                        var direction = (newPos - planet.Position).normalized;
                        newPos = planet.Position + (direction * planet.Radius);
                        break;
                    }
                }
                shipTransform.position = newPos;

            }
            for (var shipIndex = 0; shipIndex < arrivingShipData.Length; shipIndex++)
            {

                var shipData = arrivingShipData[shipIndex];
                var shipTransform = arrivingShipTransforms[shipIndex];
                var planetData = _entityManager.GetComponent<PlanetData>(shipData.TargetEntity);

                if (shipData.TeamOwnership != planetData.TeamOwnership)
                {
                    planetData.Occupants = planetData.Occupants - 1;
                    if (planetData.Occupants <= 0)
                    {
                        planetData.TeamOwnership = shipData.TeamOwnership;
                        PlanetSpawner.SetColor(shipData.TargetEntity, planetData.TeamOwnership);
                    }
                }
                else
                {
                    planetData.Occupants = planetData.Occupants + 1;
                }
                _entityManager.SetComponent(shipData.TargetEntity, planetData);
                Object.Destroy(shipTransform.gameObject);
            }

            arrivingShipData.Dispose();

        }
    }
}
