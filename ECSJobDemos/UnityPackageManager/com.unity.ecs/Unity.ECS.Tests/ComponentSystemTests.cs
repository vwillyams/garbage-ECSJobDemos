using System;
using NUnit.Framework;

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
    }
}