using UnityEngine;
using UnityEngine.ECS;
using UnityEditor;
using Unity.Collections;
using NUnit.Framework;

namespace UnityEngine.ECS.Tests
{
    public class EntityManagerTests
    {
        [Test]
        public void GetComponentObjectReturnsTheCorrectType()
        {
            var world = new World ();
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
            var world = new World ();
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
            var world = new World ();
            world.SetDefaultCapacity(3);
            var entityMan = world.CreateManager<EntityManager>();
            World.Active = world;
            
            var archetype = entityMan.CreateArchetype(typeof(EcsTestData));
            var count = 1024;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            entityMan.CreateEntity (archetype, array);
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i, array[i].index);
            }
            array.Dispose();
            
            world.Dispose();
        }        
    }
}