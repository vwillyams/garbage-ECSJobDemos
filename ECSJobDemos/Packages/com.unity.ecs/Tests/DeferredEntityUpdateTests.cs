using Unity.Jobs;
using Unity.Collections;
using NUnit.Framework;
using Unity.ECS;

namespace UnityEngine.ECS.Tests
{
    public class DeferredEntityUpdateTests : ECSTestsFixture
    {
        private const string worldName = "DeferredEntityUpdateTests";
        
        [DisableAutoCreation]
        public class TestEntitySystem : ComponentSystem
        {
            public int expectedCount = 0;
            
            public struct TestGroup
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData>  testData;
                public int Length;
            }

            [Inject] 
            public TestGroup Group;

            protected override void OnUpdate()
            {
                Assert.AreEqual( expectedCount, Group.Length );
            }
        }        

        [DisableAutoCreation]
        public class Test2EntitySystem : ComponentSystem
        {
            public int expectedCount = 0;
            
            public struct TestGroup
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData2> testData2;
                public int Length;
            }

            [Inject] 
            public TestGroup Group;

            protected override void OnUpdate()
            {
                Assert.AreEqual( expectedCount, Group.Length );
            }
        }        
        
        [DisableAutoCreation]
        public class AddTest2System : JobComponentSystem
        {
            [Inject] 
            private DeferredEntityChangeSystem deferredEntityChangeSystem;
            
            public int expectedCount = 0;
            
            public struct TestGroup
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData>  testData;
                public EntityArray entity;
                public int Length;
            }

            [Inject] 
            public TestGroup Group;
            
            struct AddTest2Component : IJobParallelFor
            {
                [ReadOnly]
                public EntityArray entity;
                public NativeQueue<AddComponentPayload<EcsTestData2>>.Concurrent addTest2UpdateQueue;

                public void Execute( int r )
                {
                    var component = new EcsTestData2(r);
                    var rc = new AddComponentPayload<EcsTestData2>()
                    {
                        entity = entity[r],
                        component = component
                    };
                    addTest2UpdateQueue.Enqueue(rc);
                }
            }
            
            protected override JobHandle OnUpdate( JobHandle depJobHandle )
            {
                Assert.AreEqual( expectedCount, Group.Length );
                var addJob = new AddTest2Component();
                addJob.entity = Group.entity;
                addJob.addTest2UpdateQueue = deferredEntityChangeSystem.GetAddComponentQueue<EcsTestData2>();

                var addJobHandle = addJob.Schedule(Group.Length, 1, depJobHandle);
                return addJobHandle;
            }
        }        
              
        [DisableAutoCreation]
        public class RemoveTest2System : JobComponentSystem
        {
            [Inject] 
            private DeferredEntityChangeSystem deferredEntityChangeSystem;
            
            public int expectedCount = 0;
            
            public struct TestGroup
            {
                [ReadOnly]
                public ComponentDataArray<EcsTestData2>  test2Data;
                public EntityArray entity;
                public int Length;
            }

            [Inject] 
            public TestGroup Group;
            
            struct RemoveTest2Component : IJobParallelFor
            {
                [ReadOnly]
                public EntityArray entity;
                public NativeQueue<Entity>.Concurrent removeTest2UpdateQueue;

                public void Execute( int r )
                {
                  removeTest2UpdateQueue.Enqueue(entity[r]);
                }
            }
            
            protected override JobHandle OnUpdate( JobHandle depJobHandle )
            {
                Assert.AreEqual( expectedCount, Group.Length );
                var removeJob = new RemoveTest2Component();
                removeJob.entity = Group.entity;
                removeJob.removeTest2UpdateQueue = deferredEntityChangeSystem.GetRemoveComponentQueue<EcsTestData2>();

                var addJobHandle = removeJob.Schedule(Group.Length, 1, depJobHandle);
                return addJobHandle;
            }
        }        
        
        [Test]
        public void FindInitialComponents()
        {
            var world = new World(worldName);
            World.Active = world;

            var entityMan = world.CreateManager<EntityManager>();
            world.CreateManager<DeferredEntityChangeSystem>();
            var testMan = world.CreateManager<TestEntitySystem>();
     
            entityMan.CreateEntity(typeof(EcsTestData));

            testMan.expectedCount = 1;
            testMan.Update();
                     
            world.Dispose();
        }
        
        [Test]
        public void FindAddedComponents()
        {
            var world = new World(worldName);
            World.Active = world;

            var entityMan = world.CreateManager<EntityManager>();
            var deferredMan = world.CreateManager<DeferredEntityChangeSystem>();
            var testMan = world.CreateManager<TestEntitySystem>();
            var test2Man = world.CreateManager<Test2EntitySystem>();
            var addTest2Man = world.CreateManager<AddTest2System>();
     
            entityMan.CreateEntity(typeof(EcsTestData));
            entityMan.CreateEntity(typeof(EcsTestData));

            testMan.expectedCount = 2;
            testMan.Update();

            test2Man.expectedCount = 0;
            test2Man.Update();
            
            addTest2Man.expectedCount = 2;
            addTest2Man.Update();
            
            deferredMan.Update();
            
            test2Man.expectedCount = 2;
            test2Man.Update();            
            
            world.Dispose();
        }
        
        [Test]
        public void DontFindRemovedComponents()
        {
            var world = new World(worldName);
            World.Active = world;

            var entityMan = world.CreateManager<EntityManager>();
            var deferredMan = world.CreateManager<DeferredEntityChangeSystem>();
            var testMan = world.CreateManager<TestEntitySystem>();
            var test2Man = world.CreateManager<Test2EntitySystem>();
            var removeTest2Man = world.CreateManager<RemoveTest2System>();
     
            entityMan.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            entityMan.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            testMan.expectedCount = 2;
            testMan.Update();

            test2Man.expectedCount = 2;
            test2Man.Update();
            
            removeTest2Man.expectedCount = 2;
            removeTest2Man.Update();
            
            deferredMan.Update();
            
            test2Man.expectedCount = 0;
            test2Man.Update();            
            
            world.Dispose();
        }        
        
    }
}
