using System;
using System.Collections.Generic;
using System.Diagnostics;
using Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Jobs;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Systems
{
    [UpdateBefore(typeof(DeferredEntityChangeSystem))]
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
            public TransformAccessArray Transforms;
            public ComponentDataArray<ShipData> Data;
            public EntityArray Entities;
        }

        struct Planets
        {
            public int Length;
            public ComponentDataArray<PlanetData> Data;
        }

        struct CalculatePositionsJob : IJobParallelForTransform
        {
            public float DeltaTime;
            [ReadOnly]
            public ComponentDataArray<ShipData> Ships;
            public EntityArray Entities;

            [ReadOnly] public ComponentDataArray<PlanetData> Planets;
            [ReadOnly] public ComponentDataFromEntity<PlanetData> TargetPlanet;
            public NativeQueue<AddComponentPayload<ShipArrivedTag>>.Concurrent AddShipArrivedTagDeferred;

            public void Execute(int index, TransformAccess transform)
            {
                var shipData = Ships[index];

                var targetPosition = TargetPlanet[shipData.TargetEntity].Position;

                var newPos = Vector3.MoveTowards(transform.position, targetPosition, DeltaTime);

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
                transform.position = newPos;
            }
        }

        [Inject]
        private DeferredEntityChangeSystem AddShipArrivedTagDeferred;
        [Inject] private Ships _ships;
        [Inject] private Planets _planets;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_ships.Length == 0)
                return inputDeps;
            var job = new CalculatePositionsJob
            {
                Ships = _ships.Data,
                Planets = _planets.Data,
                TargetPlanet = _entitymanager.GetComponentDataFromEntity<PlanetData>(),
                DeltaTime = Time.deltaTime,
                Entities = _ships.Entities,
                AddShipArrivedTagDeferred = AddShipArrivedTagDeferred.GetAddComponentQueue<ShipArrivedTag>()
            };

            return job.Schedule(_ships.Transforms, inputDeps);
        }
    }
}
