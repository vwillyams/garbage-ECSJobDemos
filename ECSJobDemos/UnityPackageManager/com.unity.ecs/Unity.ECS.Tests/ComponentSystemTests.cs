using System;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    public class ComponentSystemTests : ECSTestsFixture
    {
        [DisableAutoCreation]
        class TestSystem : ComponentSystem
        {
            public bool Created = false;
            
            protected override void OnUpdate()
            {
            }

            protected override void OnCreateManager(int capacity)
            {
                Created = true;
            }
            
            protected override void OnDestroyManager()
            {
                Created = false;        
            }
        }
        
        [DisableAutoCreation]
        class DerivedTestSystem : TestSystem
        {
            protected override void OnUpdate()
            {
            }
        }
        
        [DisableAutoCreation]
        class ThrowExceptionSystem : TestSystem
        {
            protected override void OnCreateManager(int capacity)
            {
                throw new System.Exception();
            }
            protected override void OnUpdate()
            {
            }
        }
        
        [DisableAutoCreation]
        class ScheduleJobAndDestroyArray : JobComponentSystem
        {
            NativeArray<int> test = new NativeArray<int>(10, Allocator.Persistent);

            struct Job : IJob
            {
                public NativeArray<int> test;

                public void Execute() { }
            }

            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                return new Job(){ test = test }.Schedule(inputDeps);
            }

            protected override void OnDestroyManager()
            {
                // We expect this to not throw an exception since the jobs scheduled
                // by this system should be synced before the system is destroyed
                test.Dispose();
            }
        }
        
        
        [Test]
        public void Create()
        {
            var system = World.CreateManager<TestSystem>();
            Assert.AreEqual(system, World.GetExistingManager<TestSystem>());
            Assert.IsTrue(system.Created);
        }

        [Test]
        public void CreateAndDestroy()
        {
            var system = World.CreateManager<TestSystem>();
            World.DestroyManager(system);
            Assert.AreEqual(null, World.GetExistingManager<TestSystem>());
            Assert.IsFalse(system.Created);
        }
        
        [Test]
        public void InheritedSystem()
        {
            var system = World.CreateManager<DerivedTestSystem>();
            Assert.AreEqual(system, World.GetExistingManager<DerivedTestSystem>());
            Assert.AreEqual(system, World.GetExistingManager<TestSystem>());

            World.DestroyManager(system);

            Assert.AreEqual(null, World.GetExistingManager<DerivedTestSystem>());
            Assert.AreEqual(null, World.GetExistingManager<TestSystem>());

            Assert.IsFalse(system.Created);
        }
        
        [Test]
        public void OnCreateThrowRemovesSystem()
        {
            Assert.Throws<Exception>(() => { World.CreateManager<ThrowExceptionSystem>(); });
            Assert.AreEqual(null, World.GetExistingManager<ThrowExceptionSystem>());
        }
        
        [Test]
        public void DestroySystemWhileJobUsingArrayIsRunningWorks()
        {
            var system = World.CreateManager<ScheduleJobAndDestroyArray>();
            system.Update();
            World.DestroyManager(system);
        }
    }
}