﻿using Data;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;

namespace Systems
{
    [UpdateAfter(typeof(ShipSpawnSystem))]
    public class OccupantIncreaseSystem : JobComponentSystem
    {
        float spawnCounter = 0.0f;
        float spawnInterval = 0.1f;
        int occupantsToSpawn = 100;

        struct Planets
        {
            public int Length;
            public ComponentDataArray<PlanetData> Data;
        }

        struct PlanetsOccupantsJob : IJobParallelFor
        {
            public ComponentDataArray<PlanetData> Data;
            [ReadOnly]
            public int OccupantsToSpawn;

            public void Execute(int index)
            {
                var data = Data[index];
                if (data.TeamOwnership == 0)
                    return;
                data.Occupants += OccupantsToSpawn;
                Data[index] = data;
            }
        }

        [Inject]
        Planets planets;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var deltaTime = Time.deltaTime;
            spawnCounter += deltaTime;
            if (spawnCounter < spawnInterval)
                return inputDeps;
            spawnCounter = 0.0f;

            var job = new PlanetsOccupantsJob
            {
                Data = planets.Data,
                OccupantsToSpawn = occupantsToSpawn
            };

            return job.Schedule(planets.Length, 32, inputDeps);
        }
    }
}
