using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RandomForwardRotationOnceSystem : ComponentSystem
    {
        struct RandomForwardRotationOnceGroup
        {
            public ComponentDataArray<ForwardRotation> forwardRotations;
            [ReadOnly]
            public ComponentDataArray<RandomForwardRotationOnce> randomRotationOnces;
            public EntityArray entities;
            public int Length;
        }

        [Inject] private RandomForwardRotationOnceGroup m_RandomForwardRotationOnceGroup;
    
        protected override void OnUpdate()
        {
            var entities = new NativeArray<Entity>(m_RandomForwardRotationOnceGroup.Length, Allocator.Temp);
            for (int i = 0; i < m_RandomForwardRotationOnceGroup.Length; i++)
            {
                entities[i] = m_RandomForwardRotationOnceGroup.entities[i];
                m_RandomForwardRotationOnceGroup.forwardRotations[i] = new ForwardRotation
                {
                    forward = math.normalize(new float3(Random.Range(-1, 1), Random.Range(-1,1), Random.Range(-1, 1)))
                };
            }
            for (int i = 0; i < entities.Length; i++)
            {
                EntityManager.RemoveComponent<RandomForwardRotationOnce>(entities[i]);
            }
            entities.Dispose();
        } 
    }
}
