using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.Transform;
using UnityEngine.XR.WSA;

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

