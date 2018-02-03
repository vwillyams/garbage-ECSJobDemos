using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

namespace UnityEngine.ECS.Tests
{
    public partial class SharedComponentDataTests
    {
        int oddTestValue = 34;
        int evenTestValue = 17;

        void AddEvenOddTestData()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var evenShared = new SharedData1(evenTestValue);
            var oddShared = new SharedData1(oddTestValue);
            for (int i = 0; i < 100; i++)
            {
                Entity e = m_Manager.CreateEntity(archetype);
                var testData = m_Manager.GetComponent<EcsTestData>(e);
                testData.value = i;
                m_Manager.SetComponent(e, testData);
                if ((i & 0x01) == 0)
                {
                    m_Manager.AddSharedComponent(e, evenShared);
                }
                else
                {
                    m_Manager.AddSharedComponent(e, oddShared);
                }
            }
        }

        void ActionEvenOdd(Action<int, ComponentGroup> even, Action<int, ComponentGroup> odd)
        {
            var uniqueTypes = new List<SharedData1>(10);
            var maingroup = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData1));
            maingroup.CompleteDependency();

            m_Manager.GetAllUniqueSharedComponents(uniqueTypes);

            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
            {
                var sharedData = uniqueTypes[sharedIndex];
                var group = maingroup.GetVariation(sharedData);
                int version = maingroup.GetVariationVersion(sharedData);

                if (sharedData.value == evenTestValue)
                {
                    even(version, @group);
                }

                if (sharedData.value == oddTestValue)
                {
                    odd(version, @group);
                }

                @group.Dispose();
            }

            maingroup.Dispose();
        }

        [Test]
        public void SharedComponentNoChangeVersionUnchanged()
        {
            AddEvenOddTestData();
            ActionEvenOdd((version, group) => { Assert.AreEqual(version, 1); },
                (version, group) => { Assert.AreEqual(version, 1); });
        }

        void testSourceEvenValues(int version, ComponentGroup group)
        {
            var testData = @group.GetComponentDataArray<EcsTestData>();

            Assert.AreEqual(50, testData.Length);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(i * 2, testData[i].value);
            }
        }

        void testSourceOddValues(int version, ComponentGroup group)
        {
            var testData = @group.GetComponentDataArray<EcsTestData>();

            Assert.AreEqual(50, testData.Length);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(1 + (i * 2), testData[i].value);
            }
        }

        [Test]
        public void SharedComponentNoChangeValuesUnchanged()
        {
            AddEvenOddTestData();
            ActionEvenOdd(testSourceEvenValues, testSourceOddValues);
        }

        void ChangeGroupOrder(int version, ComponentGroup group)
        {
            var entityData = @group.GetEntityArray();
            var entities = new NativeArray<Entity>(50, Allocator.Temp);
            entityData.CopyTo(new NativeSlice<Entity>(entities));

            for (int i = 0; i < 50; i++)
            {
                var e = entities[i];
                if ((i & 0x01) == 0)
                {
                    var testData2 = new EcsTestData2(i);
                    m_Manager.AddComponent(e, testData2);
                }
            }

            entities.Dispose();
        }

        [Test]
        public void SharedComponentChangeOddGroupOrderOnlyOddVersionChanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => { }, ChangeGroupOrder);
            ActionEvenOdd((version, group) => { Assert.AreEqual(version, 1); },
                (version, group) => { Assert.Greater(version, 1); });
        }

        [Test]
        public void SharedComponentChangeOddGroupOrderEvenValuesUnchanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => { }, ChangeGroupOrder);
            ActionEvenOdd(testSourceEvenValues, (version, group) => { });
        }

        void DestroyAllButOneEntityInGroup(int version, ComponentGroup group)
        {
            var entityData = @group.GetEntityArray();
            var entities = new NativeArray<Entity>(50, Allocator.Temp);
            entityData.CopyTo(new NativeSlice<Entity>(entities));

            for (int i = 0; i < 49; i++)
            {
                var e = entities[i];
                m_Manager.DestroyEntity(e);
            }

            entities.Dispose();
        }

        [Test]
        public void SharedComponentDestroyAllButOneEntityInOddGroupOnlyOddVersionChanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => { }, DestroyAllButOneEntityInGroup);
            ActionEvenOdd((version, group) => { Assert.AreEqual(version, 1); },
                (version, group) => { Assert.Greater(version, 1); });
        }

        [Test]
        public void SharedComponentDestroyAllButOneEntityInOddGroupEvenValuesUnchanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => { }, DestroyAllButOneEntityInGroup);
            ActionEvenOdd(testSourceEvenValues, (version, group) => { });
        }
    }
}