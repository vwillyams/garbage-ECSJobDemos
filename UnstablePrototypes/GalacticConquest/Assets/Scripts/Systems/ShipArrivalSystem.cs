using System.Collections.Generic;
using Data;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Jobs;

namespace Systems
{
    [UpdateAfter(typeof(ShipMovementSystem))]
    public class ShipArrivalSystem : ComponentSystem
    {
        private EntityManager _entityManager;

        public ShipArrivalSystem()
        {
            _entityManager = World.Active.GetOrCreateManager<EntityManager>();
        }

        struct Ships
        {
            public int Length;
            public ComponentArray<Transform> Transforms;
            public ComponentDataArray<ShipData> Data;
        }
        [Inject] private Ships _ships;

        protected override void OnUpdate()
        {
            var arrivingShipTransforms = new List<Transform>();
            var arrivingShipData = new NativeList<ShipData>(Allocator.Temp);

            for (var shipIndex = 0; shipIndex < _ships.Length; shipIndex++)
            {
                var shipData = _ships.Data[shipIndex];
                var shipTransform = _ships.Transforms[shipIndex];
                var targetPlanet = _entityManager.GetComponent<PlanetData>(shipData.TargetEntity);


                if (Vector3.Distance(targetPlanet.Position, shipTransform.position) <= targetPlanet.Radius * 1.01f)
                {
                    arrivingShipData.Add(shipData);
                    arrivingShipTransforms.Add(shipTransform);
                }
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
