using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

public class CollisionSystem : ComponentSystem
{
    struct Colliders
    {
        public int Length;
        public ComponentArray<SpriteRenderer> renderers;
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
            var firstBounds = colliders.renderers[i].bounds;

            for (int j = 0; j < colliders.Length; ++j)
            {
                var second = colliders.entities[j];
                var secondBounds = colliders.renderers[j].bounds;

                if (first == second)
                    continue;
                
                if (EntityManager.HasComponent<PlayerTagComponentData>(first) &&
                    EntityManager.HasComponent<AsteroidTagComponentData>(second) &&
                    firstBounds.Intersects(secondBounds))
                {
                    damageQueue.Enqueue(first);
                }

                if (EntityManager.HasComponent<BulletTagComponentData>(first) &&
                    EntityManager.HasComponent<AsteroidTagComponentData>(second) &&
                    firstBounds.Intersects(secondBounds))
                {
                    damageQueue.Enqueue(first);
                    damageQueue.Enqueue(second);
                }

                if (EntityManager.HasComponent<AsteroidTagComponentData>(first) &&
                    EntityManager.HasComponent<AsteroidTagComponentData>(second) &&
                    firstBounds.Intersects(secondBounds))
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