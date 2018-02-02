using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{
    public class RemoveDeadSystem : ComponentSystem
    {
        public struct Data
        {
            [ReadOnly] public Health Health;
        }

        protected override void OnUpdate()
        {
            var toDestroy = new List<GameObject>();
            foreach (var entity in GetEntities<Data>())
            {
                if (entity.Health.Value <= 0)
                {
                    toDestroy.Add(entity.Health.gameObject);
                }
            }

            foreach (var go in toDestroy)
            {
                Object.Destroy(go);
            }
        }
    }
}
