using Unity.Collections;
using Unity.ECS;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ECS;

namespace TwoStickHybridExample
{
    public class SyncTransformSystem : ComponentSystem
    {
        public struct Data
        {

            [ReadOnly] public Transform2D Transform;
            public Transform Output;
        }

        protected override void OnUpdate()
        {
            foreach (var entity in GetEntities<Data>())
            {
                
                float2 p = entity.Transform.Position;
                entity.Output.position = new float3(p.x, 0, p.y);
            }
        }
    }

}

