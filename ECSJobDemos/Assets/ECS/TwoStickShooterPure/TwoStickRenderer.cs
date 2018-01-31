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

namespace TwoStickExample
{
    public class TwoStickRenderer : JobComponentSystem
    {
        public struct Data
        {
            public int Length;

            [ReadOnly] public ComponentDataArray<Transform2D> Transform;
            public ComponentDataArray<TransformMatrix> Output;
        }

        [Inject] private Data m_Data;

        private struct TransformJob : IJobParallelFor
        {
            [ReadOnly] public ComponentDataArray<Transform2D> Transform;
            public ComponentDataArray<TransformMatrix> Output;

            public void Execute(int index)
            {
                float2 p = Transform[index].Position;
                float4x4 m = math.translate(new float3(p.x, 0, p.y));
                Output[index] = new TransformMatrix {matrix = m};
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new TransformJob
            {
                Transform = m_Data.Transform,
                Output = m_Data.Output
            };

            return job.Schedule(m_Data.Length, 128, inputDeps);
        }
    }

}

