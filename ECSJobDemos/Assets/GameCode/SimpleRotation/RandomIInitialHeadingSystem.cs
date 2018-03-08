using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RandomInitialHeadingSystem : ComponentSystem
    {
        struct RandomInitialHeadingGroup
        {
            public ComponentDataArray<Heading> Headings;
            [ReadOnly] public ComponentDataArray<RandomInitialHeading> RandomInitialHeadiings;
            public EntityArray Entities;
            public int Length;
        }

        [Inject] RandomInitialHeadingGroup m_Group;

        protected override void OnUpdate()
        {
            var entities = new NativeArray<Entity>(m_Group.Length, Allocator.Temp);
            for (int i = 0; i < m_Group.Length; i++)
            {
                entities[i] = m_Group.Entities[i];
                m_Group.Headings[i] = new Heading
                {
                    Value = math.normalize(new float3(Random.Range(-1, 1), Random.Range(-1,1), Random.Range(-1, 1)))
                };
                
                PostUpdateCommands.RemoveComponent<RandomInitialHeading>(m_Group.Entities[i]);
            }
        }
    }
}
