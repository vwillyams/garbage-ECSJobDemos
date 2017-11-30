using UnityEngine;
using UnityEngine.ECS;
using UnityEditor;
using NUnit.Framework;

namespace UnityEngine.ECS.Tests
{
    public class EntityManagerTests : ECSTestsFixture
    {
        [Test]
        public void GetComponentObjectReturnsTheCorrectType()
        {
            var entityMan = World.GetBehaviourManager<EntityManager>();

            var go = new GameObject();
            go.AddComponent<EcsTestComponent>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            var component = entityMan.GetComponentObject<Transform>(go.GetComponent<GameObjectEntity>().Entity);

            Assert.NotNull(component, "EntityManager.GetComponentObject returned a null object");
            Assert.AreEqual(typeof(Transform), component.GetType(), "EntityManager.GetComponentObject returned the wrong component type.");
            Assert.AreEqual(go.transform, component, "EntityManager.GetComponentObject returned a different copy of the component.");
        }

        [Test]
        public void GetComponentObjectThrowsIfComponentDoesNotExist()
        {
            var entityMan = World.GetBehaviourManager<EntityManager>();

            var go = new GameObject();
            go.AddComponent<EcsTestComponent>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            Assert.Throws<System.ArgumentException>(() => entityMan.GetComponentObject<Rigidbody>(go.GetComponent<GameObjectEntity>().Entity));
        }
    }
}