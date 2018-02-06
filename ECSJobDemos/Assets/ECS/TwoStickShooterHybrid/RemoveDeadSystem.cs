using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{
    public class RemoveDeadSystem : ComponentSystem
    {
        public struct Entities
        {
            public int Length;
            public GameObjectArray gameObjects;
            public ComponentArray<Health> healths;
        }

        [Inject] private Entities entities;

        protected override void OnUpdate()
        {
            var toDestroy = new List<GameObject>();
            for (var i = 0; i < entities.Length; ++i)
            {
                
                if (entities.healths[i].Value <= 0)
                {
                    toDestroy.Add(entities.gameObjects[i]);
                }
            }

            foreach (var go in toDestroy)
            {
                Object.Destroy(go);
            }
        }
    }
}
