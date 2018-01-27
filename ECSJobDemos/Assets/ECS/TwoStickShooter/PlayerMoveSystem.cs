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
    public class PlayerMoveSystem : ComponentSystem
    {
        public struct Data
        {
            public int Length;
            public ComponentDataArray<WorldPos> Position;
            [ReadOnly] public ComponentDataArray<PlayerInput> Input;
        }

        [InjectComponentGroup] private Data m_Data;

        protected override void OnUpdate()
        {
            float dt = Time.deltaTime;
            for (int index = 0; index < m_Data.Length; ++index)
            {
                WorldPos pos = m_Data.Position[index];

                pos.Position += dt * m_Data.Input[index].Move;
                pos.Heading = m_Data.Input[index].Shoot;

                m_Data.Position[index] = pos;
            }
        }
    }
}
