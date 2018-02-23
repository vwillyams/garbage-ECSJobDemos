using Data;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Systems
{
    public class RotationSystem : JobComponentSystem
    {
        struct Planets
        {
            public int Length;
            public ComponentDataArray<RotationData> Data;
            public TransformAccessArray Transforms;
        }

        struct RotationJob : IJobParallelForTransform
        {
            public ComponentDataArray<RotationData> Rotations;
            public void Execute(int index, TransformAccess transform)
            {
                transform.rotation = transform.rotation * Quaternion.Euler( Rotations[index].RotationSpeed);
            }
        }

        [Inject] private Planets _planets;
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new RotationJob
            {
                Rotations = _planets.Data
            };

            return job.Schedule(_planets.Transforms, inputDeps);
        }
    }
}
