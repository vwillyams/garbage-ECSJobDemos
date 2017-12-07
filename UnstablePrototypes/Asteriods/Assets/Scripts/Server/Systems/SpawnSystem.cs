using UnityEngine.ECS;
using UnityEngine;

using Unity.Mathematics;

namespace Asteriods.Server
{
    public class SpawnSystem : ComponentSystem
    {
        struct Player
        {
            public int Length;
            public ComponentDataArray<PlayerTagComponentData> players;
        }

        struct Asteroid
        {
            public int Length;
            public ComponentDataArray<AsteroidTagComponentData> asteroids;
        }

        [InjectComponentGroup]
        Player player;

        [InjectComponentGroup]
        Asteroid asteroids;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnUpdate()
        {
            if (player.players.Length == 0)
            {
                Debug.Log("SpawnSystem inside Server");
                EntityManager.CreateEntity(GameSettings.Instance().playerArchetype);

                // queue command to client;
                Asteriods.Client.SpawnSystem.spawnQueue.Enqueue(new SpawnCommand());
            }

            for (int i = asteroids.Length; i < 2; i++)
            {
                var angle = Random.Range(-0.0f, 359.0f);
                var obj = GameObject.Instantiate(GameSettings.Instance().asteroidPrefab,
                    new Vector3(Random.Range(-21.0f, 21.0f),
                        Random.Range(-15.0f, 15.0f), 0),
                    Quaternion.Euler(0, 0, angle));

                float dy = (float)(math.sin(math.radians(angle + 90)) * 0.005);
                float dx = (float)(math.cos(math.radians(angle + 90)) * 0.005);

                var steering = new SteeringComponentData(angle, dx, dy);
                EntityManager.SetComponent<SteeringComponentData>(obj.GetComponent<GameObjectEntity>().Entity, steering);
            }
        }
    }
}