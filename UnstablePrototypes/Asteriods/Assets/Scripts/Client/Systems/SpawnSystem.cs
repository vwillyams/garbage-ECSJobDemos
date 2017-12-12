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
                        Entity e = EntityManager.CreateEntity(GameSettings.Instance().asteroidClientArchetype);
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        EntityManager.SetComponent(e, new PositionComponentData(cmd.position.x, cmd.position.y));
                        EntityManager.SetComponent(e, new RotationComponentData(cmd.rotation.angle));
                        NetworkIdLookup.TryAdd(cmd.id, e);
                    } break;
                    case SpawnType.Bullet:
                    {
                        Entity e = EntityManager.CreateEntity(GameSettings.Instance().bulletClientArchetype);
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        EntityManager.SetComponent(e, new PositionComponentData(cmd.position.x, cmd.position.y));
                        EntityManager.SetComponent(e, new RotationComponentData(cmd.rotation.angle));
                        NetworkIdLookup.TryAdd(cmd.id, e);

                    } break;
                    case SpawnType.Ship:
                    {
                        Entity e = EntityManager.CreateEntity(GameSettings.Instance().playerClientArchetype);
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        EntityManager.SetComponent(e, new PositionComponentData(cmd.position.x, cmd.position.y));
                        EntityManager.SetComponent(e, new RotationComponentData(cmd.rotation.angle));
                        EntityManager.SetComponent(e, GameSettings.Instance().playerPrefab.GetComponent<ParticleEmitterComponent>().Value);
                        NetworkIdLookup.TryAdd(cmd.id, e);
                    } break;
                }
            }
        }
    }
}