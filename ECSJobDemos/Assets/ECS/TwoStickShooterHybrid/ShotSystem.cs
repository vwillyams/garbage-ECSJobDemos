using System.CodeDom;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Transform;

namespace TwoStickHybridExample
{
    public class ShotMoveSystem : ComponentSystem
    {
        struct Data
        {
            [ReadOnly] public Shot Shot;
            public Transform2D Transform;
        }

        protected override void OnUpdate()
        {
            foreach (var entity in GetEntities<Data>())
            {
                entity.Transform.Position += entity.Transform.Heading * entity.Shot.Speed;
            }
        }
    }

    [UpdateAfter(typeof(ShotMoveSystem))]
    public class ShotDestroySystem : ComponentSystem
    {
        struct Data
        {
            public Shot Shot;
        }

        protected override void OnUpdate()
        {
            float dt = Time.deltaTime;

            var toDestroy = new List<GameObject>();
            foreach (var entity in GetEntities<Data>())
            {
                var s = entity.Shot;
                s.TimeToLive -= dt;
                if (s.TimeToLive <= 0.0f)
                {
                    toDestroy.Add(s.gameObject);
                }
            }

            foreach (var go in toDestroy)
            {
                Object.Destroy(go);
            }
        }
    }

}
