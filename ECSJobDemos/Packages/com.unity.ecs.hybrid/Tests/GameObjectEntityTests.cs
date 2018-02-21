using System;
using NUnit.Framework;
using Unity.ECS;
using Unity.ECS.Hybrid;
using UnityEngine.ECS.Tests;
using UnityEngine.Jobs;

namespace UnityEngine.ECS.Hybrid.Tests
{
	public class GameObjectEntityTests : ECSTestsFixture
	{
	    GameObjectArrayInjectionHook m_GameObjectArrayInjectionHook = new GameObjectArrayInjectionHook();

	    [OneTimeSetUp]
	    public void Init()
	    {
	        InjectionHookSupport.RegisterHook(m_GameObjectArrayInjectionHook);
	    }

	    [OneTimeTearDown]
	    public void Cleanup()
	    {
	        InjectionHookSupport.UnregisterHook(m_GameObjectArrayInjectionHook);
	    }

	    [DisableAutoCreation]
	    public class GameObjectArraySystem : ComponentSystem
	    {
	        public struct Group
	        {
	            public int Length;
	            public GameObjectArray gameObjects;

	            public ComponentArray<BoxCollider> colliders;
	        }

	        [Inject]
	        public Group group;

	        protected override void OnUpdate()
	        {
	        }
	    }

	    [Test]
	    public void GameObjectArrayIsPopulated()
	    {
	        var go = new GameObject("test", typeof(BoxCollider));
	        GameObjectEntity.AddToEntityManager(m_Manager, go);

	        var manager = World.GetOrCreateManager<GameObjectArraySystem>();

	        manager.UpdateInjectedComponentGroups();

	        Assert.AreEqual(1, manager.group.Length);
	        Assert.AreEqual(go, manager.group.gameObjects[0]);
	        Assert.AreEqual(go, manager.group.colliders[0].gameObject);

	        Object.DestroyImmediate (go);
	        TearDown();
	    }

	    [DisableAutoCreation]
	    public class GameObjectArrayWithTransformAccessSystem : ComponentSystem
	    {
	        public struct Group
	        {
	            public int Length;
	            public GameObjectArray gameObjects;

	            public TransformAccessArray transforms;
	        }

	        [Inject]
	        public Group group;

	        protected override void OnUpdate()
	        {
	        }
	    }

	    [Test]
	    public void GameObjectArrayWorksWithTransformAccessArray()
	    {
	        var go = new GameObject("test");
	        GameObjectEntity.AddToEntityManager(m_Manager, go);

	        var manager = World.GetOrCreateManager<GameObjectArrayWithTransformAccessSystem>();

	        manager.UpdateInjectedComponentGroups();

	        Assert.AreEqual(1, manager.group.Length);
	        Assert.AreEqual(go, manager.group.gameObjects[0]);
	        Assert.AreEqual(go, manager.group.transforms[0].gameObject);

	        Object.DestroyImmediate (go);
	        TearDown();
	    }
	}
}
