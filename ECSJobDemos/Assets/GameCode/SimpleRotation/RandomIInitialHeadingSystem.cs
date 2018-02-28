using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;

namespace UnityEngine.ECS.SimpleRotation
{
    public class RandomInitialHeadingSystem : JobComponentSystem
    {
        struct RandomInitialHeadingGroup
        {
            public ComponentDataArray<Heading> headings;
            [ReadOnly] public ComponentDataArray<RandomInitialHeading> randomInitialHeadiings;
            public EntityArray entities;
            public int Length;
        }

        [Inject] private RandomInitialHeadingGroup m_RandomInitialHeadingGroup;
        [Inject] private EntityManager m_EntityManager;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_RandomInitialHeadingGroup.Length == 0)
                return inputDeps;

            inputDeps.Complete();
            var entities = new NativeArray<Entity>(m_RandomInitialHeadingGroup.Length, Allocator.Temp);
            for (int i = 0; i < m_RandomInitialHeadingGroup.Length; i++)
            {
                entities[i] = m_RandomInitialHeadingGroup.entities[i];
                m_RandomInitialHeadingGroup.headings[i] = new Heading
                {
                    Value = math.normalize(new float3(Random.Range(-1, 1), Random.Range(-1,1), Random.Range(-1, 1)))
                };
            }
            for (int i = 0; i < entities.Length; i++)
            {
                EntityManager.RemoveComponent<RandomInitialHeading>(entities[i]);
            }
            entities.Dispose();
            return new JobHandle();
        }
    }
}
