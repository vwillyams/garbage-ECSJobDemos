using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    public class JobComponentSystemDependencyTests : ECSTestsFixture
    {
        [DisableAutoCreation]
        public class ReadSystem1 : JobComponentSystem
        {
            public struct Inputs
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData> data;
            }

            [InjectComponentGroup] private Inputs m_Inputs;

            struct ReadJob : IJob
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData> wat;

                public void Execute()
                {
                }
            }

            public override JobHandle OnUpdate(JobHandle input)
            {
                return new ReadJob() { wat = m_Inputs.data }.Schedule(input);
            }
        }

        [DisableAutoCreation]
        public class ReadSystem2 : JobComponentSystem
        {
            public struct Inputs
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData> data;
            }

            public bool returnWrongJob = false;
            public bool ignoreInputDeps = false;

            [InjectComponentGroup] private Inputs m_Inputs;

            private struct ReadJob : IJob
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData> wat;

                public void Execute()
                {
                }
            }

            public override JobHandle OnUpdate(JobHandle input)
            {
                JobHandle h;

                var job = new ReadJob() {wat = m_Inputs.data};

                if (ignoreInputDeps)
                {
                    h = job.Schedule();
                }
                else
                {
                    h = job.Schedule(input);
                }

                return returnWrongJob ? input : h;
            }
        }

        [DisableAutoCreation]
        public class ReadSystem3 : JobComponentSystem
        {
            public struct Inputs
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData> data;
            }

            [InjectComponentGroup] private Inputs m_Inputs;

            public override JobHandle OnUpdate(JobHandle input)
            {
                return input;
            }
        }

        [DisableAutoCreation]
        public class WriteSystem : JobComponentSystem
        {
            public struct Inputs
            {
                public ComponentDataArray<EcsTestData> data;
            }

            [InjectComponentGroup] private Inputs m_Inputs;

            public override JobHandle OnUpdate(JobHandle input)
            {
                return input;
            }
        }

        [Test]
        public void ReturningWrongJobThrowsInCorrectSystemUpdate()
        {
            var entity = m_Manager.CreateEntity (typeof(EcsTestData));
            m_Manager.SetComponent(entity, new EcsTestData(42));
            ReadSystem1 rs1 = World.GetOrCreateManager<ReadSystem1>();
            ReadSystem2 rs2 = World.GetOrCreateManager<ReadSystem2>();

            rs2.returnWrongJob = true;

            rs1.Update();
            Assert.Throws<System.InvalidOperationException>(() => { rs2.Update(); });
        }

        [Test]
        public void IgnoredInputDepsThrowsInCorrectSystemUpdate()
        {
            var entity = m_Manager.CreateEntity (typeof(EcsTestData));
            m_Manager.SetComponent(entity, new EcsTestData(42));
            ReadSystem1 rs1 = World.GetOrCreateManager<ReadSystem1>();
            ReadSystem2 rs2 = World.GetOrCreateManager<ReadSystem2>();

            rs2.ignoreInputDeps = true;

            rs1.Update();
            Assert.Throws<System.InvalidOperationException>(() => { rs2.Update(); });
        }

        [Test]
        public void NotUsingDataIsHarmless()
        {
            var entity = m_Manager.CreateEntity (typeof(EcsTestData));
            m_Manager.SetComponent(entity, new EcsTestData(42));
            ReadSystem1 rs1 = World.GetOrCreateManager<ReadSystem1>();
            ReadSystem3 rs3 = World.GetOrCreateManager<ReadSystem3>();

            rs1.Update();
            rs3.Update();
        }
    }
}