using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Samples.Common
{
    public class MoveForwardSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct MoveForwardPosition : IJobParallelFor
        {
            public ComponentDataArray<Position> positions;
            [ReadOnly] public ComponentDataArray<Rotation> rotations;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                positions[i] = new Position
                {
                    Value = positions[i].Value + (dt * moveSpeeds[i].speed * math.forward(rotations[i].Value))
                };
            }
        }
        
        ComponentGroup m_MoveForwardGroup;

        protected override void OnCreateManager(int capacity)
        {
            m_MoveForwardGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(MoveForward)),
                ComponentType.ReadOnly(typeof(Rotation)),
                ComponentType.Subtractive(typeof(LocalRotation)),
                ComponentType.ReadOnly(typeof(MoveSpeed)),
                typeof(Position));
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardPositionJob = new MoveForwardPosition
            {
                positions = m_MoveForwardGroup.GetComponentDataArray<Position>(),
                rotations = m_MoveForwardGroup.GetComponentDataArray<Rotation>(),
                moveSpeeds = m_MoveForwardGroup.GetComponentDataArray<MoveSpeed>(),
                dt = Time.deltaTime
            };

            return moveForwardPositionJob.Schedule(m_MoveForwardGroup.CalculateLength(), 64, inputDeps);
        }
    }
}
