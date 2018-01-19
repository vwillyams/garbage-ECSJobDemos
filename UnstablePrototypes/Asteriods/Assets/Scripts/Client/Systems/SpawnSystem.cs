//#define STRESS_TEST_PARTICLES
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

            SpawnQueue = new NativeQueue<SpawnCommand>(Allocator.Persistent);
            NetworkIdLookup = new NativeHashMap<int, Entity>(1024, Allocator.Persistent);

            Debug.Assert(SpawnQueue.IsCreated);
            Debug.Assert(NetworkIdLookup.IsCreated);
			#if STRESS_TEST_PARTICLES
            var emitterType = EntityManager.CreateArchetype(typeof(PositionComponentData), typeof(RotationComponentData), typeof(ParticleEmitterComponentData));
            var emitterData = ClientSettings.Instance().playerPrefab.GetComponent<ParticleEmitterComponent>().Value;
            int numEmitters = 0;
            for (int yp = 0; yp < Screen.height; yp += 20)
            {
                for (int xp = 0; xp < Screen.width; xp += 100)
                {
                    Entity e = EntityManager.CreateEntity(emitterType);
                    EntityManager.SetComponent(e, new PositionComponentData(xp, yp));
                    EntityManager.SetComponent(e, emitterData);
                    ++numEmitters;
                }
            }
            Debug.Log("Running " + numEmitters + " systems, " + (numEmitters * emitterData.particlesPerSecond) + " particles / second");
			#endif
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

                Entity et;
                if (NetworkIdLookup.TryGetValue(cmd.id, out et) && EntityManager.Exists(et))
                {
                    Debug.Log("id = " + cmd.id  + " already exists");
                    continue;
                }

                switch((SpawnType)cmd.type)
                {
                    case SpawnType.Asteroid:
                    {
                        Entity e = EntityManager.CreateEntity(ClientSettings.Instance().asteroidClientArchetype);
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        EntityManager.SetComponent(e, new PositionComponentData(cmd.position.x, cmd.position.y));
                        EntityManager.SetComponent(e, new RotationComponentData(cmd.rotation.angle));
                        NetworkIdLookup.TryAdd(cmd.id, e);
                    } break;
                    case SpawnType.Bullet:
                    {
                        Entity e = EntityManager.CreateEntity(ClientSettings.Instance().bulletClientArchetype);
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        EntityManager.SetComponent(e, new PositionComponentData(cmd.position.x, cmd.position.y));
                        EntityManager.SetComponent(e, new RotationComponentData(cmd.rotation.angle));
                        NetworkIdLookup.TryAdd(cmd.id, e);

                    } break;
                    case SpawnType.Ship:
                    {
                        Entity e = EntityManager.CreateEntity(ClientSettings.Instance().shipArchetype);
                        EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(cmd.id));
                        EntityManager.SetComponent(e, new PositionComponentData(cmd.position.x, cmd.position.y));
                        EntityManager.SetComponent(e, new RotationComponentData(cmd.rotation.angle));
                        EntityManager.SetComponent(e, ClientSettings.Instance().playerPrefab.GetComponent<ParticleEmitterComponent>().Value);

                        NetworkIdLookup.TryAdd(cmd.id, e);
                    } break;
                }
            }
        }
    }
}
