using UnityEngine.ECS;
using UnityEngine;

using Unity.Collections;
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


        public NativeQueue<SpawnCommand> spawnQueue;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            spawnQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            Debug.Assert(spawnQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (spawnQueue.IsCreated)
                spawnQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            if (player.players.Length == 0)
            {
                Debug.Log("SpawnSystem inside Server");
                var e = EntityManager.CreateEntity(GameSettings.Instance().playerArchetype);

                var pos = new PositionComponentData(5f, 5f);
                var rot = new RotationComponentData(90f);

                EntityManager.SetComponent<PositionComponentData>(e, pos);
                EntityManager.SetComponent<RotationComponentData>(e, rot);
                EntityManager.SetComponent<VelocityComponentData>(e, new VelocityComponentData(0, 0));

                int id = 0;
                spawnQueue.Enqueue(
                    new SpawnCommand(id, (int)SpawnType.Ship, pos, rot));
            }

            for (int i = asteroids.Length; i < 2; i++)
            {
                var angle = Random.Range(-0.0f, 359.0f);
                var obj = GameObject.Instantiate(GameSettings.Instance().asteroidPrefab,
                    new Vector3(Random.Range(-21.0f, 21.0f),
                        Random.Range(-15.0f, 15.0f), 0),
                    Quaternion.Euler(0, 0, angle));

                float dy = (float)(math.sin(math.radians(angle + 90)) * 0.015);
                float dx = (float)(math.cos(math.radians(angle + 90)) * 0.015);

                var steering = new VelocityComponentData(dx, dy);
                EntityManager.SetComponent<VelocityComponentData>(obj.GetComponent<GameObjectEntity>().Entity, steering);
            }
        }
    }
}