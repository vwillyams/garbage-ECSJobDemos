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
    
    public class EntityManagerTests : ECSTestsFixture
    {
        [Test]
        public void GetComponentObjectReturnsTheCorrectType()
        {
            var go = new GameObject();
            go.AddComponent<EcsTestComponent>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            var component = m_Manager.GetComponentObject<Transform>(go.GetComponent<GameObjectEntity>().Entity);

            Assert.NotNull(component, "EntityManager.GetComponentObject returned a null object");
            Assert.AreEqual(typeof(Transform), component.GetType(), "EntityManager.GetComponentObject returned the wrong component type.");
            Assert.AreEqual(go.transform, component, "EntityManager.GetComponentObject returned a different copy of the component.");
        }

        [Test]
        public void GetComponentObjectThrowsIfComponentDoesNotExist()
        {
            var go = new GameObject();
            go.AddComponent<EcsTestComponent>();
            // Execute in edit mode is not enabled so this has to be called manually right now
            go.GetComponent<GameObjectEntity>().OnEnable();

            Assert.Throws<System.ArgumentException>(() => m_Manager.GetComponentObject<Rigidbody>(go.GetComponent<GameObjectEntity>().Entity));
        }
            
        [Test]
        public void IncreaseEntityCapacity()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var count = 1024;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            m_Manager.CreateEntity (archetype, array);
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i, array[i].Index);
            }
            array.Dispose();
        }

        [Test]
        public void FoundComponentInterface()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData),typeof(EcsFooTest));
            var count = 1024;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            m_Manager.CreateEntity (archetype, array);

            var fooTypes = m_Manager.GetAssignableComponentTypes(typeof(IEcsFooInterface));
            Assert.AreEqual(1,fooTypes.Count);
            Assert.AreEqual(typeof(EcsFooTest),fooTypes[0]);
            
            var barTypes = m_Manager.GetAssignableComponentTypes(typeof(IEcsBarInterface));
            Assert.AreEqual(0,barTypes.Count);
            
            array.Dispose();
        }
    }
}