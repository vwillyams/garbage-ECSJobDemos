using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RotationSpeedSystem : JobComponentSystem
    {
        struct RotationSpeedGroup
        {
            public ComponentDataArray<TransformRotation> rotations;
            [ReadOnly]
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            public int Length;
        }

        [Inject] private RotationSpeedGroup m_RotationSpeedGroup;
    
        [ComputeJobOptimization]
        struct RotationSpeedRotation : IJobParallelFor
        {
            public ComponentDataArray<TransformRotation> rotations;
            [ReadOnly]
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                var speed = rotationSpeeds[i].speed;
                if (speed > 0.0f)
                {
                    rotations[i] = new TransformRotation
                    {
                        rotation = math.mul(math.normalize(rotations[i].rotation), math.axisAngle(math.up(),speed*dt))
                    };
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationSpeedRotationJob = new RotationSpeedRotation
            {
                rotations = m_RotationSpeedGroup.rotations,
                rotationSpeeds = m_RotationSpeedGroup.rotationSpeeds,
                dt = Time.deltaTime
            };
            return rotationSpeedRotationJob.Schedule(m_RotationSpeedGroup.Length, 64, inputDeps);
        } 
    }
}
