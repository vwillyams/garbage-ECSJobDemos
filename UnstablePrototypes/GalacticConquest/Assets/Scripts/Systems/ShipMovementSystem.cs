using System;
using Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
    [UpdateAfter(typeof(ShipArrivalSystem))]
    public class ShipMovementSystem : JobComponentSystem
    {
        public ShipMovementSystem()
        {
            _entitymanager = World.Active.GetOrCreateManager<EntityManager>();
        }

        EntityManager _entitymanager;

        struct Ships
        {
            public int Length;
            public ComponentDataArray<Position> Transforms;
            public ComponentDataArray<ShipData> Data;
            public EntityArray Entities;
        }

        struct Planets
        {
            public int Length;
            public ComponentDataArray<PlanetData> Data;
        }

        struct CalculatePositionsJob : IJobParallelFor
        {
            public float DeltaTime;
            [ReadOnly]
            public ComponentDataArray<ShipData> Ships;
            public EntityArray Entities;
            public ComponentDataArray<Position> Transforms;

            [ReadOnly] public ComponentDataArray<PlanetData> Planets;
            [ReadOnly] public ComponentDataFromEntity<PlanetData> TargetPlanet;
            public NativeQueue<AddComponentPayload<ShipArrivedTag>>.Concurrent AddShipArrivedTagDeferred;

            public void Execute(int index)
            {
                var shipData = Ships[index];

                var targetPosition = TargetPlanet[shipData.TargetEntity].Position;
                var transform = Transforms[index];

                var newPos = Vector3.MoveTowards(transform.Value, targetPosition, DeltaTime * 4.0f);

                for (var planetIndex = 0; planetIndex < Planets.Length; planetIndex++)
                {
                    var planet = Planets[planetIndex];
                    if (Vector3.Distance(newPos, planet.Position) < planet.Radius)
                    {
                        if (planet.Position == targetPosition)
                        {
                            AddShipArrivedTagDeferred.Enqueue(new AddComponentPayload<ShipArrivedTag>(Entities[index], new ShipArrivedTag()));
                        }
                        var direction = (newPos - planet.Position).normalized;
                        newPos = planet.Position + (direction * planet.Radius);
                        break;
                    }
                }
                transform.Value = newPos;
                Transforms[index] = transform;
            }
        }

        [Inject]
        DeferredEntityChangeSystem AddShipArrivedTagDeferred;
        [Inject]
        Ships _ships;
        [Inject]
        Planets _planets;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_ships.Length == 0)
                return inputDeps;
            var job = new CalculatePositionsJob
            {
                Ships = _ships.Data,
                Planets = _planets.Data,
                TargetPlanet = GetComponentDataFromEntity<PlanetData>(),
                DeltaTime = Time.deltaTime,
                Entities = _ships.Entities,
                Transforms = _ships.Transforms,
                AddShipArrivedTagDeferred = AddShipArrivedTagDeferred.GetAddComponentQueue<ShipArrivedTag>()
            };

            return job.Schedule(_ships.Length, 32, inputDeps);
        }
    }
}
