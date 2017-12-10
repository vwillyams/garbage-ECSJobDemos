using UnityEngine.ECS;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Client
{
    public class SpawnSystem : ComponentSystem
    {
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
            for (int i = 0, c = spawnQueue.Count; i < c; i++)
            {
                var cmd = spawnQueue.Dequeue();

                switch((SpawnType)cmd.type)
                {
                    case SpawnType.Asteroid:
                    {
                        GameObject.Instantiate(
                            GameSettings.Instance().asteroidPrefab, 
                            new Vector3(cmd.position.x, cmd.position.y, 0), 
                            Quaternion.Euler(0f, 0f, cmd.rotation.angle));
                    } break;
                    case SpawnType.Bullet:
                    case SpawnType.Ship:
                    {
                        GameObject.Instantiate(
                            GameSettings.Instance().playerPrefab, 
                            new Vector3(cmd.position.x, cmd.position.y, 0), 
                            Quaternion.Euler(0f, 0f, cmd.rotation.angle));
                    } break;
                }
            }
        }
    }
}