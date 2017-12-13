using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;
using Unity.Mathematics;

namespace Asteriods.Server
{
    public class CollisionSystem : ComponentSystem
    {
        struct Colliders
        {
            public int Length;
            public ComponentDataArray<CollisionSphereComponentData> spheres;
            public ComponentDataArray<PositionComponentData> positions;

            public EntityArray entities;
        }

        [InjectComponentGroup]
        Colliders colliders;


        // NOTE (michalb): queue is needed because we cant add a component during iteration of the CompoenentGroup
        NativeQueue<Entity> damageQueue;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            damageQueue = new NativeQueue<Entity>(128, Allocator.Persistent);
            Debug.Assert(damageQueue.IsCreated);
        }

        override protected void OnDestroyManager()
        {
            if (damageQueue.IsCreated)
                damageQueue.Dispose();
        }

        override protected void OnUpdate()
        {
            if (colliders.Length <= 0)
                return;

            for (int i = 0; i < colliders.Length; ++i)
            {
                var first = colliders.entities[i];
                var firstRadius = colliders.spheres[i].radius;
                float2 firstPos = new float2(colliders.positions[i].x, colliders.positions[i].y);

                for (int j = 0; j < colliders.Length; ++j)
                {
                    var second = colliders.entities[j];
                    var secondRadius = colliders.spheres[j].radius;
                    float2 secondPos = new float2(colliders.positions[j].x, colliders.positions[j].y);

                    if (first == second)
                        continue;

                    float2 diff = firstPos-secondPos;
                    float distSq = math.dot(diff, diff);
                    bool intersects = distSq <= (firstRadius+secondRadius)*(firstRadius+secondRadius);

                    if (EntityManager.HasComponent<PlayerTagComponentData>(first) &&
                        EntityManager.HasComponent<AsteroidTagComponentData>(second) &&
                        intersects)
                    {
                        damageQueue.Enqueue(first);
                    }

                    if (EntityManager.HasComponent<BulletTagComponentData>(first) &&
                        EntityManager.HasComponent<AsteroidTagComponentData>(second) &&
                        intersects)
                    {
                        damageQueue.Enqueue(first);
                        damageQueue.Enqueue(second);
                    }

                    if (EntityManager.HasComponent<AsteroidTagComponentData>(first) &&
                        EntityManager.HasComponent<AsteroidTagComponentData>(second) &&
                        intersects)
                    {
                        // TODO (michalb): make them bounce off
                    }
                }

            }

            if (damageQueue.Count > 0)
            {
                int count = damageQueue.Count;
                for (int i = 0; i < count; ++i)
                {
                    var e = damageQueue.Dequeue();
                    if (!EntityManager.HasComponent<DamageCompmonentData>(e))
                        EntityManager.AddComponent<DamageCompmonentData>(e, new DamageCompmonentData(100));
                }
                Debug.Assert(damageQueue.Count == 0);
            }
        }
    }
}