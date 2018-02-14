using Unity.Collections;
using Unity.ECS;
using Unity.Mathematics;

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
                    value = math.normalize(new float3(Random.Range(-1, 1), Random.Range(-1,1), Random.Range(-1, 1)))
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
