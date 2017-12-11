using System.Collections.Generic;

using UnityEngine;
using UnityEngine.ECS;

using Unity.Collections;

namespace Asteriods.Server
{
    [UpdateAfter(typeof(CollisionSystem))]
    public class DamageSystem : ComponentSystem
    {
        // NOTE (michalb): should we distinguish between the different entiteis that damage?
        struct DamagedEntities
        {
            public int Length;
            public ComponentDataArray<DamageCompmonentData> damage;
            public ComponentDataArray<NetworkIdCompmonentData> ids;
            public EntityArray refs;
        }

        [InjectComponentGroup]
        DamagedEntities entities;

        [Inject]
        NetworkMessageSystem m_NetworkMessageSystem;

        override protected void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);
        }

        override protected void OnDestroyManager()
        {
        }

        override protected void OnUpdate()
        {
            NativeList<Entity> delete = new NativeList<Entity>(Allocator.Temp);

            if (entities.Length > 0)
            {
                for (int i = 0; i < entities.Length; ++i)
                {
                    delete.Add(entities.refs[i]);
                    m_NetworkMessageSystem.DespawnQueue.Enqueue(new DespawnCommand(entities.ids[i].id));
                }
            }

            for (int i = 0; i < delete.Length; i++)
            {
                EntityManager.DestroyEntity(delete[i]);
            }

            delete.Dispose();
        }
    }

}