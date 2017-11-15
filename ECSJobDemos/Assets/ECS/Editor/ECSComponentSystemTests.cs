using UnityEngine.ECS;
using NUnit.Framework;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;

namespace UnityEngine.ECS.Tests
{
    public class PureEcsTestSystem : ComponentSystem
    {
        public struct DataAndEntites
        {
            public ComponentDataArray<EcsTestData> Data;
            public EntityArray                     Entities;
            public int                             Length;
        }

        [InjectComponentGroup] 
        public DataAndEntites Group;

        public override void OnUpdate() { base.OnUpdate (); }
    }

    public class PureReadOnlySystem : ComponentSystem
    {
        public struct Datas
        {
            [ReadOnly]
            public ComponentDataArray<EcsTestData> Data;
        }

        [InjectComponentGroup] 
        public Datas Group;

        public override void OnUpdate() { base.OnUpdate (); }
    }


	public class ECSComponentSystemTests : ECSFixture
	{
        [Test]
        public void ReadOnlyTuples()
        {
            var readOnlySystem = DependencyManager.GetBehaviourManager<PureReadOnlySystem> ();

            var go = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go, new EcsTestData(2));

            readOnlySystem.OnUpdate ();
            Assert.AreEqual (2, readOnlySystem.Group.Data[0].value);
            Assert.Throws<System.InvalidOperationException>(()=> { readOnlySystem.Group.Data[0] = new EcsTestData(); });
        }
        
        [Test]
        public void RemoveComponentTupleTracking()
        {
            var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();

            var go0 = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go0, new EcsTestData(10));

            var go1 = m_Manager.CreateEntity ();
            m_Manager.AddComponent (go1, new EcsTestData(20));

            pureSystem.OnUpdate ();
            Assert.AreEqual (2, pureSystem.Group.Length);
            Assert.AreEqual (10, pureSystem.Group.Data[0].value);
            Assert.AreEqual (20, pureSystem.Group.Data[1].value);

            m_Manager.RemoveComponent<EcsTestData> (go0);

            pureSystem.OnUpdate ();
            Assert.AreEqual (1, pureSystem.Group.Length);
            Assert.AreEqual (20, pureSystem.Group.Data[0].value);

            m_Manager.RemoveComponent<EcsTestData> (go1);
            pureSystem.OnUpdate ();
            Assert.AreEqual (0, pureSystem.Group.Length);
        }

        [Test]
        public void EntityTupleTracking()
        {
            var pureSystem = DependencyManager.GetBehaviourManager<PureEcsTestSystem> ();

            var go = m_Manager.CreateEntity (new ComponentType[0]);
            m_Manager.AddComponent (go, new EcsTestData(2));

            pureSystem.OnUpdate ();
            Assert.AreEqual (1, pureSystem.Group.Length);
            Assert.AreEqual (1, pureSystem.Group.Data.Length);
            Assert.AreEqual (1, pureSystem.Group.Entities.Length);
            Assert.AreEqual (2, pureSystem.Group.Data[0].value);
            Assert.AreEqual (go, pureSystem.Group.Entities[0]);
        }
	}
}