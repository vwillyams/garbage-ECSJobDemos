using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.TestTools;

namespace UnityEngine.ECS.Tests
{
    public class EntityTransactionTests : ECSTestsFixture
    {
        ComponentGroup m_Group;

        public EntityTransactionTests()
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled, "JobDebugger must be enabled for these tests");
        }

        [SetUp]
        public void Setup()
        {
            m_Group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            // Archetypes can't be created on a job
            m_Manager.CreateArchetype(typeof(EcsTestData));
        }

        struct CreateEntityJob : IJob
        {
            public EntityTransaction entities;
            public NativeList<Entity> createdEntities;

            public void Execute()
            {
                var entity = entities.CreateEntity(ComponentType.Create<EcsTestData>());
                entities.SetComponent(entity, new EcsTestData(42));

                createdEntities.Add(entity);
            }
        }


        [Test]
        public void CreateEntitiesChainedJob()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginTransaction();
            job.createdEntities = new NativeList<Entity>(0, Allocator.TempJob);

            m_Manager.DidScheduleCreationJob(job.Schedule(m_Manager.GetCreationDependency()));
            m_Manager.DidScheduleCreationJob(job.Schedule(m_Manager.GetCreationDependency()));

            m_Manager.CommitTransaction();

            Assert.AreEqual(2, m_Group.CalculateLength());
            Assert.AreEqual(42, m_Group.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(42, m_Group.GetComponentDataArray<EcsTestData>()[1].value);

            Assert.IsTrue(m_Manager.Exists(job.createdEntities[0]));
            Assert.IsTrue(m_Manager.Exists(job.createdEntities[1]));

            job.createdEntities.Dispose();
        }


        [Test]
        public void CreateEntityAfterCreationJobAutomaticallyCommitsTransaction()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginTransaction();
            job.createdEntities = new NativeList<Entity>(0, Allocator.TempJob);

            m_Manager.DidScheduleCreationJob(job.Schedule(m_Manager.GetCreationDependency()));

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            Assert.AreEqual(2, m_Group.CalculateLength());
            Assert.AreEqual(42, m_Group.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(0, m_Group.GetComponentDataArray<EcsTestData>()[1].value);

            Assert.IsTrue(m_Manager.Exists(job.createdEntities[0]));
            Assert.IsTrue(m_Manager.Exists(entity));
            Assert.AreNotEqual(entity, job.createdEntities[0]);

            job.createdEntities.Dispose();
        }


	    //@TODO: Test for All the incorrect corner cases... job dependencies, chaining of creation, get/set component that is already alive etc
    }
}
