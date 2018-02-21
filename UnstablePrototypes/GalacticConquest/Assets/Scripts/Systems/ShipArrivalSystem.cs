using Data;
using Unity.Collections;
using Unity.ECS;
using UnityEngine;
using Unity.Rendering.Hybrid;

namespace Systems
{
    [UpdateAfter(typeof(MeshInstanceRendererSystem))]
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
            public ComponentDataArray<ShipData> Data;
            public EntityArray Entities;
            public ComponentDataArray<ShipArrivedTag> Tag;
        }
        [Inject] private Ships _ships;

        protected override void OnUpdate()
        {
            if (_ships.Length == 0)
                return;

            var arrivingShipTransforms = new NativeList<Entity>(Allocator.Temp);
            var arrivingShipData = new NativeList<ShipData>(Allocator.Temp);

            for (var shipIndex = 0; shipIndex < _ships.Length; shipIndex++)
            {
                var shipData = _ships.Data[shipIndex];
                var shipEntity = _ships.Entities[shipIndex];
                arrivingShipData.Add(shipData);
                arrivingShipTransforms.Add(shipEntity);
            }

            HandleArrivedShips(arrivingShipData, arrivingShipTransforms);

            arrivingShipTransforms.Dispose();
            arrivingShipData.Dispose();
        }

        private void HandleArrivedShips(NativeList<ShipData> arrivingShipData, NativeList<Entity> arrivingShipEntities)
        {
            for (var shipIndex = 0; shipIndex < arrivingShipData.Length; shipIndex++)
            {

                var shipData = arrivingShipData[shipIndex];
                var planetData = _entityManager.GetComponentData<PlanetData>(shipData.TargetEntity);

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
                _entityManager.SetComponentData(shipData.TargetEntity, planetData);
            }
            _entityManager.DestroyEntity(arrivingShipEntities);
        }
    }
}
