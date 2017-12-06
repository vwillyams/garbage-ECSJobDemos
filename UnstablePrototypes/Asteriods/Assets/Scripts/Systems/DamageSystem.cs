using System.Collections.Generic;

using UnityEngine;
using UnityEngine.ECS;

[UpdateAfter(typeof(CollisionSystem))]
public class DamageSystem : ComponentSystem
{
    // NOTE (michalb): should we distinguish between the different entiteis that damage?
    struct DamagedEntities
    {
        public int Length;
        public ComponentDataArray<DamageCompmonentData> damage;
        public ComponentArray<Transform> transforms;
    }

    [InjectComponentGroup]
    DamagedEntities entities;

    override protected void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
    }

    override protected void OnUpdate()
    {
        List<GameObject> remove = new List<GameObject>();

        if (entities.Length > 0)
        {
            for (int i = 0; i < entities.Length; ++i)
                if (entities.transforms[i].gameObject != null)
                    remove.Add(entities.transforms[i].gameObject);
                    //GameObject.Destroy(entities.transforms[i].gameObject);
        }

        foreach (var go in remove)
            GameObject.Destroy(go);
    }
}