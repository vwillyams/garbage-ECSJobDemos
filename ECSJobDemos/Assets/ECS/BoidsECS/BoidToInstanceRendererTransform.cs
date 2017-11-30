using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.ECS.Rendering;
using Unity.Mathematics;

namespace BoidSimulations
{
    class matrix_math_util
    {
        const float epsilon = 0.000001F;

        public static float3x3 identity3
        {
            get { return new float3x3(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1)); }
        }
        public static float4x4 identity4
        {
            get { return new float4x4(new float4(1, 0, 0, 0), new float4(0, 1, 0, 0), new float4(0, 0, 1, 0), new float4(0, 0, 0, 1)); }
        }

        public static float4x4 LookRotationToMatrix(float3 position, float3 forward, float3 up)
        {
            float3x3 rot = LookRotationToMatrix(forward, up);

            float4x4 matrix;
            matrix.m0 = new float4(rot.m0, 0.0F);
            matrix.m1 = new float4(rot.m1, 0.0F);
            matrix.m2 = new float4(rot.m2, 0.0F);
            matrix.m3 = new float4(position, 1.0F);

            return matrix;
        }

        public static float3x3 LookRotationToMatrix(float3 forward, float3 up)
        {
            float3 z = forward;
            // compute u0
            float mag = math.length(z);
            if (mag < epsilon)
                return identity3;
            z /= mag;

            float3 x = math.cross(up, z);
            mag = math.length(x);
            if (mag < epsilon)
                return identity3;
            x /= mag;

            float3 y = math.cross(z, x);
            float yLength = math.length(y);
            if (yLength < 0.9F || yLength > 1.1F)
                return identity3;

            return new float3x3(x, y, z);
        }
    }

    //[DisableAutoCreation]
    [ComputeJobOptimizationAttribute(Accuracy.Med, Support.Relaxed)]
    struct BoidToInstanceRendererTransform : IJobProcessComponentData<BoidData, InstanceRendererTransform>, IAutoComponentSystemJob
    {
        // @TODO: Remove after burst fixes
        float burstWorkaround;
        
        public void Prepare()
        {
        }

        public void Execute(ref BoidData boid, ref InstanceRendererTransform transform)
        {
            transform.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new float3(0, 1, 0));
            
            // To see burst compile failure, replace with line below
            // transform.matrix = matrix_math_util.LookRotationToMatrix(boid.position, boid.forward, new Vector3(0, 1, 0));
        }
    }

}