﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Samples.Common;
using Unity.Transforms;

namespace Samples.Common
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

        [Inject] RotationSpeedResetSphereGroup m_RotationSpeedResetSphereGroup;

        struct RotationSpeedGroup
        {
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            [ReadOnly] public ComponentDataArray<Position> positions;
            public int Length;
        }

        [Inject] RotationSpeedGroup m_RotationSpeedGroup;

        [ComputeJobOptimization]
        struct RotationSpeedResetSphereRotation : IJobParallelFor
        {
            public ComponentDataArray<RotationSpeed> rotationSpeeds;
            [ReadOnly] public ComponentDataArray<Position> positions;

            [ReadOnly] public ComponentDataArray<RotationSpeedResetSphere> rotationSpeedResetSpheres;
            [ReadOnly] public ComponentDataArray<Radius> spheres;
            [ReadOnly] public ComponentDataArray<Position> rotationSpeedResetSpherePositions;

            public void Execute(int i)
            {
                var center = positions[i].Value;

                for (int positionIndex = 0; positionIndex < rotationSpeedResetSpheres.Length; positionIndex++)
                {
                    if (math.distance(rotationSpeedResetSpherePositions[positionIndex].Value, center) < spheres[positionIndex].radius)
                    {
                        rotationSpeeds[i] = new RotationSpeed
                        {
                            Value = rotationSpeedResetSpheres[positionIndex].speed
                        };
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
            return rotationSpeedResetSphereRotationJob.Schedule(m_RotationSpeedGroup.Length, 32, inputDeps);
        }
    }
}
