using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS.SimpleBounds;
using Unity.Transforms;

namespace UnityEngine.ECS.SimpleRotation
{
    [UpdateBefore(typeof(RotationSpeedSystem))]
    public class RotationSpeedResetSphereSystem : JobComponentSystem
    {
        struct RotationSpeedResetSphereGroup
        {
            [ReadOnly] public ComponentDataArray<RotationSpeedResetSphere> rotationSpeedResetSpheres;
            [ReadOnly] public ComponentDataArray<Radius> spheres;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] private RotationSpeedResetSphereGroup m_RotationSpeedResetSphereGroup;

        struct RotationSpeedGroup
        {
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] private RotationSpeedGroup m_RotationSpeedGroup;

        [ComputeJobOptimization]
        struct RotationSpeedResetSphereRotation : IJob
        {
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            [ReadOnly] public ComponentDataArray<RotationSpeedResetSphere> rotationSpeedResetSpheres;
            [ReadOnly] public ComponentDataArray<Radius> spheres;
            [ReadOnly] public ComponentDataArray<Position> rotationSpeedResetSpherePositions;
            [ReadOnly] public ComponentDataArray<Position> positions;

            public void Execute()
            {
                for (int i = 0; i < rotationSpeedResetSpheres.Length; i++)
                {
                    var center = rotationSpeedResetSpherePositions[i].Value;
                    var radius = spheres[i].radius;
                    var speed = rotationSpeedResetSpheres[i].speed;

                    for (int positionIndex = 0; positionIndex < positions.Length; positionIndex++)
                    {
                        if (math.distance(positions[positionIndex].Value, center) < radius)
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
                spheres = m_RotationSpeedResetSphereGroup.spheres,
                rotationSpeeds = m_RotationSpeedGroup.rotationSpeeds,
                rotationSpeedResetSpherePositions = m_RotationSpeedResetSphereGroup.positions,
                positions = m_RotationSpeedGroup.positions
            };
            return rotationSpeedResetSphereRotationJob.Schedule(inputDeps);
        }
    }
}
