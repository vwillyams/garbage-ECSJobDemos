using Unity.ECS;
using UnityEngine;

using Unity.Collections;
using Unity.Mathematics;
using Unity.Multiplayer;

using Exception = System.Exception;

namespace Asteriods.Server
{
    public class SpawnSystem : ComponentSystem
    {
        [Inject]
        NetworkStateSystem m_NetworkStateSystem;
        struct Players
        {
            public int Length;
            public ComponentDataArray<PlayerInputComponentData> inputs;
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

        [Inject]
        Players players;

        [Inject]
        Asteroid asteroids;

        public NativeQueue<SpawnCommand> OutgoingSpawnQueue;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
            OutgoingSpawnQueue = new NativeQueue<SpawnCommand>(Allocator.Persistent);
            Debug.Assert(OutgoingSpawnQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (OutgoingSpawnQueue.IsCreated)
                OutgoingSpawnQueue.Dispose();
        }

        struct BulletSpawnInfo
        {
            public int nid;
            public PositionComponentData pos;
            public RotationComponentData rot;
        }

        override protected void OnUpdate()
        {
            var spawnList = new NativeList<BulletSpawnInfo>(Allocator.Temp);
            for (int i = 0, s = players.Length; i < s; ++i)
            {
                if (players.inputs[i].shoot == 1)
                {
                    var bsi = new BulletSpawnInfo()
                    {
                        nid = m_NetworkStateSystem.GetNextNetworkId(),
                        pos = players.positions[i],
                        rot = players.rotations[i]
                    };

                    spawnList.Add(bsi);
                }
            }

            var maxAsteroids = players.Length * 2;

            for (int i = asteroids.Length; i < maxAsteroids; i++)
            {
                var id = m_NetworkStateSystem.GetNextNetworkId();
                var pos = new PositionComponentData(Random.Range(0, GameSettings.mapWidth), Random.Range(0, GameSettings.mapHeight));
                var rot = new RotationComponentData(Random.Range(-0.0f, 359.0f));

                float dx = (float)(-math.sin(math.radians(rot.angle)) * ServerSettings.Instance().asteroidVelocity);
                float dy = (float)(math.cos(math.radians(rot.angle)) * ServerSettings.Instance().asteroidVelocity);

                var e = EntityManager.CreateEntity(ServerSettings.Instance().asteroidArchetype);

                EntityManager.SetComponentData(e, pos);
                EntityManager.SetComponentData(e, rot);
                EntityManager.SetComponentData(e, new EntityTypeComponentData(){ Type = (int)SpawnType.Asteroid});
                EntityManager.SetComponentData(e, new VelocityComponentData(dx, dy));
                EntityManager.SetComponentData(e, new NetworkIdCompmonentData(id));
                EntityManager.SetComponentData(
                    e, new CollisionSphereComponentData(ServerSettings.Instance().asteroidRadius));

                OutgoingSpawnQueue.Enqueue(
                    new SpawnCommand(id, (int)SpawnType.Asteroid, pos, rot));
            }

            for (int i = 0, s = spawnList.Length; i < s && players.Length > 0; ++i)
            {
                var bsi = spawnList[i];

                PositionComponentData p = bsi.pos;
                RotationComponentData r = bsi.rot;

                var e = EntityManager.CreateEntity(ServerSettings.Instance().bulletArchetype);

                EntityManager.SetComponentData(e, new EntityTypeComponentData(){ Type = (int)SpawnType.Bullet});
                EntityManager.SetComponentData(e, p);
                EntityManager.SetComponentData(e, r);

                float angle = r.angle;
                float dx = 0;
                float dy = 0;

                dx -= math.sin(math.radians(angle)) * ServerSettings.Instance().bulletVelocity;
                dy += math.cos(math.radians(angle)) * ServerSettings.Instance().bulletVelocity;

                EntityManager.SetComponentData(e, new BulletAgeComponentData(1.5f));
                EntityManager.SetComponentData<VelocityComponentData>(e, new VelocityComponentData(dx, dy));
                EntityManager.SetComponentData<NetworkIdCompmonentData>(e, new NetworkIdCompmonentData(bsi.nid));
                EntityManager.SetComponentData<CollisionSphereComponentData>(
                    e, new CollisionSphereComponentData(ServerSettings.Instance().bulletRadius));

                OutgoingSpawnQueue.Enqueue(
                    new SpawnCommand(bsi.nid, (int)SpawnType.Bullet, p, r));
            }

            spawnList.Dispose();
        }

        public void SpawnPlayer(Entity e)
        {
            var pos = new PositionComponentData(GameSettings.mapWidth / 2, GameSettings.mapHeight / 2);
            var rot = new RotationComponentData(90f);

            EntityManager.SetComponentData<PositionComponentData>(e, pos);
            EntityManager.SetComponentData<RotationComponentData>(e, rot);
            EntityManager.SetComponentData<VelocityComponentData>(e, new VelocityComponentData(0, 0));

            var netId = EntityManager.GetComponentData<NetworkIdCompmonentData>(e);
            Debug.Log("spawn plaeyr for netid " + netId.id);

            EntityManager.AddComponentData<CollisionSphereComponentData>(
                e, new CollisionSphereComponentData(ServerSettings.Instance().playerRadius));

            OutgoingSpawnQueue.Enqueue(
                new SpawnCommand(netId.id, (int)SpawnType.Ship, pos, rot));
        }
    }
}
