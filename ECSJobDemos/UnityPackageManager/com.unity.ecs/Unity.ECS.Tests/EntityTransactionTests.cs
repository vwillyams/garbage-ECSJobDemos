﻿using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Unity.Jobs;
using Unity.Collections;

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
        public override void Setup()
        {
            base.Setup();

            m_Group = m_Manager.CreateComponentGroup(typeof(EcsTestData));

            // Archetypes can't be created on a job
            m_Manager.CreateArchetype(typeof(EcsTestData));
        }

        struct CreateEntityAddToListJob : IJob
        {
            public EntityTransaction entities;
            public NativeList<Entity> createdEntities;

            public void Execute()
            {
                var entity = entities.CreateEntity(ComponentType.Create<EcsTestData>());
                entities.SetComponent(entity, new EcsTestData(42));
                Assert.AreEqual(42, entities.GetComponent<EcsTestData>(entity).value);

                createdEntities.Add(entity);
            }
        }

        struct CreateEntityJob : IJob
        {
            public EntityTransaction entities;

            public void Execute()
            {
                var entity = entities.CreateEntity(ComponentType.Create<EcsTestData>());
                entities.SetComponent(entity, new EcsTestData(42));
                Assert.AreEqual(42, entities.GetComponent<EcsTestData>(entity).value);
            }
        }


        [Test]
        public void CreateEntitiesChainedJob()
        {
            var job = new CreateEntityAddToListJob();
            job.entities = m_Manager.BeginTransaction();
            job.createdEntities = new NativeList<Entity>(0, Allocator.TempJob);

            m_Manager.EntityTransactionDependency = job.Schedule(m_Manager.EntityTransactionDependency);
            m_Manager.EntityTransactionDependency = job.Schedule(m_Manager.EntityTransactionDependency);

            m_Manager.CommitTransaction();

            Assert.AreEqual(2, m_Group.CalculateLength());
            Assert.AreEqual(42, m_Group.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(42, m_Group.GetComponentDataArray<EcsTestData>()[1].value);

            Assert.IsTrue(m_Manager.Exists(job.createdEntities[0]));
            Assert.IsTrue(m_Manager.Exists(job.createdEntities[1]));

            job.createdEntities.Dispose();
        }


        [Test]
        public void CommitAfterNotRegisteredTransactionJobLogsError()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginTransaction();

            /*var jobHandle =*/ job.Schedule(m_Manager.EntityTransactionDependency);

            // Commit transaction expects an error not exception otherwise errors might occurr after a system has completed...
            TestTools.LogAssert.Expect(LogType.Error, new Regex("EntityTransaction job has not been registered"));
            m_Manager.CommitTransaction();
        }

        [Test]
        public void CreateEntityAfterCreationJobAutomaticallyCommitsTransaction()
        {
            var job = new CreateEntityAddToListJob();
            job.entities = m_Manager.BeginTransaction();
            job.createdEntities = new NativeList<Entity>(0, Allocator.TempJob);

            m_Manager.EntityTransactionDependency = job.Schedule(m_Manager.EntityTransactionDependency);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            Assert.AreEqual(2, m_Group.CalculateLength());
            Assert.AreEqual(42, m_Group.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(0, m_Group.GetComponentDataArray<EcsTestData>()[1].value);

            Assert.IsTrue(m_Manager.Exists(job.createdEntities[0]));
            Assert.IsTrue(m_Manager.Exists(entity));
            Assert.AreNotEqual(entity, job.createdEntities[0]);

            job.createdEntities.Dispose();
        }

        [Test]
        public void AccessInTransactionEntityFromEntityManagerThrows()
        {
            var transaction = m_Manager.BeginTransaction();
            var entity = transaction.CreateEntity(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() => { m_Manager.GetComponent<EcsTestData>(entity); });
            Assert.Throws<ArgumentException>(() => { var temp = m_Manager.GetComponentDataFromEntity<EcsTestData>()[entity]; });
            Assert.Throws<ArgumentException>(() => { m_Manager.SetComponent<EcsTestData>(entity, new EcsTestData()); });
            Assert.Throws<ArgumentException>(() => { m_Manager.Exists(entity); });
        }


        [Test]
        public void AccessExistingEntityFromTransactionThrows()
        {
            var transaction = m_Manager.BeginTransaction();
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() => { transaction.GetComponent<EcsTestData>(entity); });
            Assert.Throws<ArgumentException>(() => { transaction.SetComponent<EcsTestData>(entity, new EcsTestData()); });
            Assert.Throws<ArgumentException>(() => { transaction.Exists(entity); });
        }

        [Test]
        public void MissingJobCreationDependency()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginTransaction();

            var jobHandle = job.Schedule();
            Assert.Throws<InvalidOperationException>(() => { job.Schedule(); });

            jobHandle.Complete();
        }

        [Test]
        public void CreationJobAndMainThreadNotAllowedInParallel()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginTransaction();

            var jobHandle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { job.entities.CreateEntity(typeof(EcsTestData)); });

            jobHandle.Complete();
        }

        [Test]
        public void CreatingEntitiesBeyondCapacityInTransactionThrows()
        {
            var arch = m_Manager.CreateArchetype(typeof(EcsTestData));

            var transaction = m_Manager.BeginTransaction();
            var entities = new NativeArray<Entity>(1000, Allocator.Persistent);
            Assert.Throws<InvalidOperationException>(() => { transaction.CreateEntity(arch, entities); });
            entities.Dispose();
        }
    }
}