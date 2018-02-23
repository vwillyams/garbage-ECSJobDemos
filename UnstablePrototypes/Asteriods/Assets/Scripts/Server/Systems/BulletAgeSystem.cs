using UnityEngine;
using Unity.Entities;

using Unity.Collections;

namespace Asteriods.Server
{
    public class BulletAgeSystem : ComponentSystem
    {
        [Inject]
        EntityManager m_EntityManager;
        [Inject]
        NetworkMessageSystem m_NetworkMessageSystem;

        struct Bullets
        {
            public int Length;
            public ComponentDataArray<BulletAgeComponentData> age;
            [ReadOnly]
            public ComponentDataArray<NetworkIdCompmonentData> ids;
            public EntityArray entities;
        }

        [Inject]
        Bullets bullets;

        override protected void OnUpdate()
        {
            NativeList<Entity> toDelete = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < bullets.Length; ++i)
            {
                var age = bullets.age[i];
                age.age += Time.deltaTime;
                if (age.age > age.maxAge)
                {
                    toDelete.Add(bullets.entities[i]);
                    m_NetworkMessageSystem.DespawnQueue.Enqueue(new DespawnCommand(bullets.ids[i].id));
                }
                bullets.age[i] = age;
            }
            for (int i = 0; i < toDelete.Length; ++i)
                m_EntityManager.DestroyEntity(toDelete[i]);
            toDelete.Dispose();
        }
    }
}
