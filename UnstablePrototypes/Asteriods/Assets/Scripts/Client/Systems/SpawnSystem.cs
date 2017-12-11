using UnityEngine.ECS;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Client
{
    public class SpawnSystem : ComponentSystem
    {
        public NativeQueue<SpawnCommand> SpawnQueue;
        public NativeHashMap<int, Entity> NetworkIdLookup;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            SpawnQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            NetworkIdLookup = new NativeHashMap<int, Entity>(1024, Allocator.Persistent);

            Debug.Assert(SpawnQueue.IsCreated);
            Debug.Assert(NetworkIdLookup.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (SpawnQueue.IsCreated)
                SpawnQueue.Dispose();
            if (NetworkIdLookup.IsCreated)
                NetworkIdLookup.Dispose();
        }
        override protected void OnUpdate()
        {
            for (int i = 0, c = SpawnQueue.Count; i < c; ++i)
            {
                // TODO (michalb): add some nice debugging for this

                var cmd = SpawnQueue.Dequeue();
                switch((SpawnType)cmd.type)
                {
                    case SpawnType.Asteroid:
                    {
                        var go = GameObject.Instantiate(
                            GameSettings.Instance().asteroidPrefab, 
                            new Vector3(cmd.position.x, cmd.position.y, 0), 
                            Quaternion.Euler(0f, 0f, cmd.rotation.angle));

                        var e = go.GetComponent<GameObjectEntity>().Entity;
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        NetworkIdLookup.TryAdd(cmd.id, e);
                    } break;
                    case SpawnType.Bullet:
                    {
                        var go = GameObject.Instantiate(
                            GameSettings.Instance().bulletPrefab, 
                            new Vector3(cmd.position.x, cmd.position.y, 0), 
                            Quaternion.Euler(0f, 0f, cmd.rotation.angle));

                        var e = go.GetComponent<GameObjectEntity>().Entity;
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        NetworkIdLookup.TryAdd(cmd.id, e);
                        GameObject.Destroy(go, 1.5f);

                    } break;
                    case SpawnType.Ship:
                    {
                        var go = GameObject.Instantiate(
                            GameSettings.Instance().playerPrefab, 
                            new Vector3(cmd.position.x, cmd.position.y, 0), 
                            Quaternion.Euler(0f, 0f, cmd.rotation.angle));

                        var e = go.GetComponent<GameObjectEntity>().Entity;
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        NetworkIdLookup.TryAdd(cmd.id, e);
                    } break;
                }
            }
        }
    }
}