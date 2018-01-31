using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    [UpdateBefore(typeof(RotationSpeedSystem))]
    public class RotationSpeedResetSphereSystem : JobComponentSystem
    {
        struct RotationSpeedResetSphereGroup
        {
            [ReadOnly]
            public ComponentDataArray<RotationSpeedResetSphere> rotationSpeedResetSpheres;
            [ReadOnly]
            public ComponentDataArray<TransformPosition> positions;
            public int Length;
        }

        [Inject] private RotationSpeedResetSphereGroup m_RotationSpeedResetSphereGroup;
        
        struct RotationSpeedGroup
        {
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            [ReadOnly]
            public ComponentDataArray<TransformPosition> positions;
            public int Length;
        }

        [Inject] private RotationSpeedGroup m_RotationSpeedGroup;
    
        [ComputeJobOptimization]
        struct RotationSpeedResetSphereRotation : IJob
        {
            [ReadOnly]
            public ComponentDataArray<RotationSpeedResetSphere> rotationSpeedResetSpheres;
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            [ReadOnly]
            public ComponentDataArray<TransformPosition> rotationSpeedResetSpherePositions;
            [ReadOnly]
            public ComponentDataArray<TransformPosition> positions;
        
            public void Execute()
            {
                for (int i = 0; i < rotationSpeedResetSpheres.Length; i++)
                {
                    var center = rotationSpeedResetSpherePositions[i].position;
                    var radius = rotationSpeedResetSpheres[i].radius;
                    var speed = rotationSpeedResetSpheres[i].speed;

                    for (int positionIndex = 0; positionIndex < positions.Length; positionIndex++)
                    {
                        if (math.distance(positions[positionIndex].position, center) < radius)
                        {
                            rotationSpeeds[positionIndex] = new RotationSpeed
                            {
                                speed = speed
                            };
                        }
                    }
                }
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationSpeedResetSphereRotationJob = new RotationSpeedResetSphereRotation
            {
                rotationSpeedResetSpheres = m_RotationSpeedResetSphereGroup.rotationSpeedResetSpheres,
                rotationSpeeds = m_RotationSpeedGroup.rotationSpeeds,
                rotationSpeedResetSpherePositions = m_RotationSpeedResetSphereGroup.positions,
                positions = m_RotationSpeedGroup.positions
            };
            return rotationSpeedResetSphereRotationJob.Schedule(inputDeps);
        } 
    }
}
