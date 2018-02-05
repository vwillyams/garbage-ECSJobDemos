using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEngine.ECS.SimpleRotation;
using UnityEngine.ECS.Transform;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RandomInitialHeadingSystem : ComponentSystem
    {
        struct RandomInitialHeadingGroup
        {
            public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<RandomInitialHeading> randomInitialHeadiings;
            public EntityArray entities;
            public int Length;
        }

        [Inject] private RandomInitialHeadingGroup m_RandomInitialHeadingGroup;
    
        protected override void OnUpdate()
        {
            var entities = new NativeArray<Entity>(m_RandomInitialHeadingGroup.Length, Allocator.Temp);
            for (int i = 0; i < m_RandomInitialHeadingGroup.Length; i++)
            {
                entities[i] = m_RandomInitialHeadingGroup.entities[i];
                m_RandomInitialHeadingGroup.headings[i] = new Heading
                {
                    forward = math.normalize(new float3(Random.Range(-1, 1), Random.Range(-1,1), Random.Range(-1, 1)))
                };
            }
            for (int i = 0; i < entities.Length; i++)
            {
                EntityManager.RemoveComponent<RandomInitialHeading>(entities[i]);
            }
            entities.Dispose();
        } 
    }
}
