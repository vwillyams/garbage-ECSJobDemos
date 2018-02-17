using Unity.Collections;
using Unity.ECS;
using Unity.Jobs;
using UnityEngine.ECS.SimpleMovement;
using UnityEngine.ECS.Transform2D;

namespace UnityEngine.ECS.SimpleMovement2D
{
    public class MoveForward2DSystem : JobComponentSystem
    {
        [ComputeJobOptimization]
        struct MoveForwardPosition : IJobParallelFor
        {
            public ComponentDataArray<Position2D> positions;
            [ReadOnly] public ComponentDataArray<Heading2D> headings;
            [ReadOnly] public ComponentDataArray<MoveSpeed> moveSpeeds;
            public float dt;
        
            public void Execute(int i)
            {
                positions[i] = new Position2D { position = positions[i].position + (dt * moveSpeeds[i].speed * headings[i].heading) };
            }
        }

        ComponentGroup m_ComponentGroup;

        protected override void OnCreateManager(int capacity)
        {
            m_ComponentGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(MoveForward)),
                ComponentType.ReadOnly(typeof(Heading2D)),
                ComponentType.ReadOnly(typeof(MoveSpeed)),
                typeof(Position2D));
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardPositionJob = new MoveForwardPosition
            {
                positions = m_ComponentGroup.GetComponentDataArray<Position2D>(),
                headings = m_ComponentGroup.GetComponentDataArray<Heading2D>(),
                moveSpeeds = m_ComponentGroup.GetComponentDataArray<MoveSpeed>(),
                dt = Time.deltaTime
            };

            return moveForwardPositionJob.Schedule(m_ComponentGroup.CalculateLength(), 64, inputDeps);
        }
    }
}
