using UnityEngine.ECS;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Client
{
    public class SpawnSystem : ComponentSystem
    {
        public NativeQueue<SpawnCommand> spawnQueue;
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
            for (int i = 0, c = spawnQueue.Count; i < c; i++)
            {
                var cmd = spawnQueue.Dequeue();
                Debug.Log("SpawnSystem inside Client" + cmd);

                switch((SpawnType)cmd.type)
                {
                    case SpawnType.Asteroid:
                    case SpawnType.Bullet:
                    case SpawnType.Ship:
                    {
                        var obj = GameObject.Instantiate(
                            GameSettings.Instance().playerPrefab, 
                            new Vector3(cmd.position.x, cmd.position.y, 0), 
                            Quaternion.EulerAngles(0f, 0f, cmd.rotation.angle));
                    } break;
                }
            }
        }
    }
}