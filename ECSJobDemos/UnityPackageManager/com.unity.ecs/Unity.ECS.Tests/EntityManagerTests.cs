using UnityEngine;
using UnityEngine.ECS;
using UnityEditor;
using Unity.Collections;
using NUnit.Framework;
using Unity.ECS;

namespace UnityEngine.ECS.Tests
{
    interface IEcsFooInterface
    {
        int value { get; set; }
        
    }
    public struct EcsFooTest : IComponentData, IEcsFooInterface
    {
        public int value { get; set; }

        public EcsFooTest(int inValue) { value = inValue; }
    }
    
    interface IEcsBarInterface
    {
        int value { get; set; }
        
    }
    public struct EcsBarTest : IComponentData, IEcsBarInterface
    {
        public int value { get; set; }

        public EcsBarTest(int inValue) { value = inValue; }
    }

    public class EcsFooTestComponent : ComponentDataWrapper<EcsFooTest> { }
    
    public class EntityManagerTests
    {
        private const string worldName = "GetComponentTest";
        [Test]
        public void GetComponentObjectReturnsTheCorrectType()
        {
            var world = new World (worldName);
            var entityMan = world.CreateManager<EntityManager>();
            World.Active = world;

            var go = new GameObject();
            go.AddComponent<EcsTestComponent>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            var component = entityMan.GetComponentObject<Transform>(go.GetComponent<GameObjectEntity>().Entity);

            Assert.NotNull(component, "EntityManager.GetComponentObject returned a null object");
            Assert.AreEqual(typeof(Transform), component.GetType(), "EntityManager.GetComponentObject returned the wrong component type.");
            Assert.AreEqual(go.transform, component, "EntityManager.GetComponentObject returned a different copy of the component.");
            
            world.Dispose();
        }

        [Test]
        public void GetComponentObjectThrowsIfComponentDoesNotExist()
        {
            var world = new World (worldName);
            var entityMan = world.CreateManager<EntityManager>();
            World.Active = world;

            var go = new GameObject();
            go.AddComponent<EcsTestComponent>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            Assert.Throws<System.ArgumentException>(() => entityMan.GetComponentObject<Rigidbody>(go.GetComponent<GameObjectEntity>().Entity));
            
            world.Dispose();
        }
            
        [Test]
        public void IncreaseEntityCapacity()
        {
            var world = new World (worldName);
            world.SetDefaultCapacity(3);
            var entityMan = world.CreateManager<EntityManager>();
            World.Active = world;
            
            var archetype = entityMan.CreateArchetype(typeof(EcsTestData));
            var count = 1024;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            entityMan.CreateEntity (archetype, array);
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i, array[i].Index);
            }
            array.Dispose();
            
            world.Dispose();
        }

        [Test]
        public void FoundComponentInterface()
        {
            var world = new World (worldName);
            world.SetDefaultCapacity(3);
            var entityMan = world.CreateManager<EntityManager>();
            World.Active = world;
            
            var archetype = entityMan.CreateArchetype(typeof(EcsTestData),typeof(EcsFooTest));
            var count = 1024;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            entityMan.CreateEntity (archetype, array);

            var fooTypes = entityMan.GetAssignableComponentTypes(typeof(IEcsFooInterface));
            Assert.AreEqual(1,fooTypes.Count);
            Assert.AreEqual(typeof(EcsFooTest),fooTypes[0]);
            
            var barTypes = entityMan.GetAssignableComponentTypes(typeof(IEcsBarInterface));
            Assert.AreEqual(0,barTypes.Count);
            
            array.Dispose();
            world.Dispose();
        }
    }
}