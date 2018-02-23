using UnityEngine;
using Unity.Entities;

using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace Asteriods.Server
{
    public class CollisionSystem : ComponentSystem
    {
        struct PlayerColliders
        {
            public int Length;
            public ComponentDataArray<CollisionSphereComponentData> spheres;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<PlayerTagComponentData> tag;

            public EntityArray entities;
        }

        [Inject]
        PlayerColliders playerColliders;

        struct BulletColliders
        {
            public int Length;
            public ComponentDataArray<CollisionSphereComponentData> spheres;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<BulletTagComponentData> tag;

            public EntityArray entities;
        }

        [Inject]
        BulletColliders bulletColliders;

        struct AsteroidColliders
        {
            public int Length;
            public ComponentDataArray<CollisionSphereComponentData> spheres;
            public ComponentDataArray<PositionComponentData> positions;
            public ComponentDataArray<AsteroidTagComponentData> tag;

            public EntityArray entities;
        }

        [Inject]
        AsteroidColliders asteroidColliders;

        // NOTE (michalb): queue is needed because we cant add a component during iteration of the CompoenentGroup
        NativeQueue<Entity> damageQueue;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            damageQueue = new NativeQueue<Entity>(Allocator.Persistent);
            Debug.Assert(damageQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (damageQueue.IsCreated)
                damageQueue.Dispose();
        }

        struct AsteroidData
        {
            public AsteroidData(float x, float y, float r, Entity ent)
            {
                position = new float2(x, y);
                radius = r;
                entity = ent;
            }
            public float2 position;
            public float radius;
            public Entity entity;
        }

        struct CollisionJob : IJobParallelFor
        {
            [ReadOnly]
            public BulletColliders bulletColliders;
            [ReadOnly]
            public NativeArray<AsteroidData> asteroidColliders;
            public NativeQueue<Entity>.Concurrent damageQueue;

            public void Execute(int i)
            {
                var firstRadius = bulletColliders.spheres[i].radius;
                float2 firstPos = new float2(bulletColliders.positions[i].x, bulletColliders.positions[i].y);

                for (int j = 0; j < asteroidColliders.Length; ++j)
                {
                    var secondRadius = asteroidColliders[j].radius;
                    float2 secondPos = asteroidColliders[j].position;

                    if (Intersect(firstRadius, secondRadius, firstPos, secondPos))
                    {
                        // Asteroid receives damage.
                        damageQueue.Enqueue(asteroidColliders[j].entity);
                    }
                }
            }
        }

        override protected void OnUpdate()
        {
            NativeQueue<Entity>.Concurrent concurrentDamageQueue = damageQueue;

            var job = new CollisionJob();

            job.bulletColliders = bulletColliders;
            // Copy asteroids to a separate struct since the safety system is type based and detects false aliasing
            var asteroidData = new NativeArray<AsteroidData>(asteroidColliders.Length, Allocator.Temp);
            for (int i = 0; i < asteroidData.Length; ++i)
            {
                asteroidData[i] = new AsteroidData(asteroidColliders.positions[i].x, asteroidColliders.positions[i].y, asteroidColliders.spheres[i].radius, asteroidColliders.entities[i]);
            }
            job.asteroidColliders = asteroidData;
            job.damageQueue = concurrentDamageQueue;
            var jobHandle = job.Schedule(bulletColliders.Length, 8);
            jobHandle.Complete();

            // check all asteroids against players (TODO: add asteroids VS. asteroids when required).
            /*
            for (int i = 0; i < asteroidColliders.Length; ++i)
            {
                var firstRadius = asteroidColliders.spheres[i].radius;
                float2 firstPos = new float2(asteroidColliders.positions[i].x, asteroidColliders.positions[i].y);

                for (int j = 0; j < playerColliders.Length; ++j)
                {
                    var secondRadius = playerColliders.spheres[j].radius;
                    float2 secondPos = new float2(playerColliders.positions[j].x, playerColliders.positions[j].y);

                    if (Intersect(firstRadius, secondRadius, firstPos, secondPos))
                    {
                        // Player receives damage.
                        concurrentDamageQueue.Enqueue(playerColliders.entities[j]);
                    }
                }
            }
            */

            asteroidData.Dispose();

            if (damageQueue.Count > 0)
            {
                int count = damageQueue.Count;
                for (int i = 0; i < count; ++i)
                {
                    var e = damageQueue.Dequeue();
                    if (!EntityManager.HasComponent<DamageCompmonentData>(e))
                        EntityManager.AddComponentData(e, new DamageCompmonentData(100));
                }
                Debug.Assert(damageQueue.Count == 0);
            }
        }
        private static bool Intersect(float firstRadius, float secondRadius, float2 firstPos, float2 secondPos)
        {
            float2 diff = firstPos-secondPos;
            float distSq = math.dot(diff, diff);

            return distSq <= (firstRadius+secondRadius)*(firstRadius+secondRadius);
        }
    }
}
