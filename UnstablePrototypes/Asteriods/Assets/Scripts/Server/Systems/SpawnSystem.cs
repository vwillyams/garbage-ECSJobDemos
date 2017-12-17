using UnityEngine.ECS;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;

using Exception = System.Exception;

namespace Asteriods.Server
{
    public class SpawnSystem : ComponentSystem
    {
        int m_SpawnId;
        struct Player
        {
            public int Length;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<RotationComponentData> rotations;
            public ComponentDataArray<VelocityComponentData> velocities;
            ComponentDataArray<PlayerTagComponentData> tags;
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

        public NativeQueue<SpawnCommand> IncommingSpawnQueue;
        public NativeQueue<SpawnCommand> OutgoingSpawnQueue;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            OutgoingSpawnQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            IncommingSpawnQueue = new NativeQueue<SpawnCommand>(128, Allocator.Persistent);
            Debug.Assert(OutgoingSpawnQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (OutgoingSpawnQueue.IsCreated)
                OutgoingSpawnQueue.Dispose();
            if (IncommingSpawnQueue.IsCreated)
                IncommingSpawnQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            if (player.Length == 0)
            {
                var e = EntityManager.CreateEntity(GameSettings.Instance().playerArchetype);

                var id = m_SpawnId++;
                var pos = new PositionComponentData(GameSettings.Instance().mapWidth / 2, GameSettings.Instance().mapHeight / 2);
                var rot = new RotationComponentData(90f);

                EntityManager.SetComponent<PositionComponentData>(e, pos);
                EntityManager.SetComponent<RotationComponentData>(e, rot);
                EntityManager.SetComponent<VelocityComponentData>(e, new VelocityComponentData(0, 0));
                EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(id));
                EntityManager.SetComponent<CollisionSphereComponentData>(
                    e, new CollisionSphereComponentData(GameSettings.Instance().playerRadius));

                OutgoingSpawnQueue.Enqueue(
                    new SpawnCommand(id, (int)SpawnType.Ship, pos, rot));
            }

            for (int i = asteroids.Length; i < 2; i++)
            {
                var id = m_SpawnId++;
                var pos = new PositionComponentData(Random.Range(0, GameSettings.Instance().mapWidth), Random.Range(0, GameSettings.Instance().mapHeight));
                var rot = new RotationComponentData(Random.Range(-0.0f, 359.0f));

                float dx = (float)(-math.sin(math.radians(rot.angle)) * GameSettings.Instance().asteroidVelocity);
                float dy = (float)(math.cos(math.radians(rot.angle)) * GameSettings.Instance().asteroidVelocity);

                var e = EntityManager.CreateEntity(GameSettings.Instance().asteroidArchetype);

                EntityManager.SetComponent<PositionComponentData>(e, pos);
                EntityManager.SetComponent<RotationComponentData>(e, rot);
                EntityManager.SetComponent<VelocityComponentData>(e, new VelocityComponentData(dx, dy));
                EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(id));
                EntityManager.SetComponent<CollisionSphereComponentData>(
                    e, new CollisionSphereComponentData(GameSettings.Instance().asteroidRadius));

                OutgoingSpawnQueue.Enqueue(
                    new SpawnCommand(id, (int)SpawnType.Asteroid, pos, rot));
            }

            for (int i = 0, c = IncommingSpawnQueue.Count; i < c && player.Length > 0; ++i)
            {
                var id = m_SpawnId++;
                var cmd = IncommingSpawnQueue.Dequeue();

                // TODO(michalb): Add lifetime to the bullet!

                // create entity seems to invalidate the player array.
                PositionComponentData p = default(PositionComponentData);
                RotationComponentData r = default(RotationComponentData);
                try
                {
                    p = player.positions[i];
                    r = player.rotations[i];
                }
                catch(Exception ex)
                {
                    Debug.LogException(ex);
                }

                var e = EntityManager.CreateEntity(GameSettings.Instance().bulletArchetype);

                EntityManager.SetComponent<PositionComponentData>(e, p);
                EntityManager.SetComponent<RotationComponentData>(e, r);

                float angle = r.angle; // player.rotations[i].angle;
                float dx = 0;
                float dy = 0;

                dx -= math.sin(math.radians(angle)) * GameSettings.Instance().bulletVelocity;
                dy += math.cos(math.radians(angle)) * GameSettings.Instance().bulletVelocity;

                EntityManager.SetComponent(e, new BulletAgeComponentData(1.5f));
                EntityManager.SetComponent<VelocityComponentData>(e, new VelocityComponentData(dx, dy));
                EntityManager.SetComponent<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(id));
                EntityManager.SetComponent<CollisionSphereComponentData>(
                    e, new CollisionSphereComponentData(GameSettings.Instance().bulletRadius));

                OutgoingSpawnQueue.Enqueue(
                    new SpawnCommand(id, (int)SpawnType.Bullet, p, r));
            }
        }
    }
}
