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
    [UpdateAfter(typeof(ShipSpawnSystem))]
    public class ShipMovementSystem : JobComponentSystem
    {
        struct Ships
        {
            public int Length;
            public TransformAccessArray Transforms;
            public ComponentDataArray<ShipData> Data;
        }

        struct Planets
        {
            public int Length;
            public ComponentArray<Transform> Transforms;
            public ComponentDataArray<PlanetData> Data;
        }

        struct CalculatePositionsJob : IJobParallelForTransform
        {
            public float DeltaTime;
            public ComponentDataArray<ShipData> Ships;

            [ReadOnly] public ComponentDataArray<PlanetData> Planets;

            public void Execute(int index, TransformAccess transform)
            {
                var shipData = Ships[index];

                var targetPosition = shipData.TargetEntityPosition;

                var newPos = Vector3.MoveTowards(transform.position, targetPosition, DeltaTime);

                for (var planetIndex = 0; planetIndex < Planets.Length; planetIndex++)
                {
                    var planet = Planets[planetIndex];
                    if (Vector3.Distance(newPos, planet.Position) < planet.Radius)
                    {
                        var direction = (newPos - planet.Position).normalized;
                        newPos = planet.Position + (direction * planet.Radius);
                        break;
                    }
                }
                transform.position = newPos;
            }
        }

        [Inject] private Ships _ships;
        [Inject] private Planets _planets;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new CalculatePositionsJob
            {
                Ships = _ships.Data,
                Planets = _planets.Data,
                DeltaTime = Time.deltaTime,
            };

            return job.Schedule(_ships.Transforms, inputDeps);
        }
    }
}
