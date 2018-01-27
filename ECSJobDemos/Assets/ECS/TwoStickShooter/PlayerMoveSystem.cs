using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.XR.WSA;

namespace TwoStickExample
{

    public class PlayerMoveSystem : JobComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentDataArray<WorldPos> Position;
            [ReadOnly]
            public ComponentDataArray<PlayerInput> Input;
        }

        [InjectComponentGroup] private Data m_Data;

        public struct Job : IJobParallelFor
        {
            public ComponentDataArray<WorldPos> Position;
            [ReadOnly]
            public ComponentDataArray<PlayerInput> Input;
            public float DeltaTime;

            public void Execute(int index)
            {
                WorldPos pos = Position[index];

                pos.Position += DeltaTime * Input[index].Move;
                pos.Heading = Input[index].Shoot;

                Position[index] = pos;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new Job
            {
                Position = m_Data.Position,
                Input = m_Data.Input,
                DeltaTime = Time.deltaTime
            };

            return job.Schedule(m_Data.Length, 64, inputDeps);
        }
    }
}
