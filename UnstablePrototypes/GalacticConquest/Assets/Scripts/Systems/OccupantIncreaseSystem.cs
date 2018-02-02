using Data;
using UnityEngine;
using UnityEngine.ECS;

namespace Systems
{
    [UpdateAfter(typeof(ShipSpawnSystem))]
    public class OccupantIncreaseSystem : ComponentSystem
    {
        private float spawnCounter = 0.0f;
        private float spawnInterval = 0.1f;
        private int occupantsToSpawn = 100;

        struct Planets
        {
            public int Length;
            public ComponentArray<Transform> Transforms;
            public ComponentDataArray<PlanetData> Data;
        }

        [Inject] private Planets _planets;
        protected override void OnUpdate()
        {
            spawnCounter += Time.deltaTime;
            if (spawnCounter < spawnInterval)
                return;
            spawnCounter = 0.0f;

            for (var i = 0; i < _planets.Length; ++i)
            {
                var data = _planets.Data[i];
                if (data.TeamOwnership == 0)
                    continue;
                data.Occupants += occupantsToSpawn;
                _planets.Data[i] = data;
            }
        }
    }
}
