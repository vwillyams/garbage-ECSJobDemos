﻿using System;
using System.Threading;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UnityEngine.ECS;
using Unity.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using NUnit.Framework.Internal.Execution;

namespace UnityEngine.ECS.Tests
{
    public class SharedComponentDataTests : ECSTestsFixture
    {
        struct SharedData1 : ISharedComponentData
        {
            public int value;

            public SharedData1(int val) { value = val; }
        }

        struct SharedData2 : ISharedComponentData
        {
            public int value;

            public SharedData2(int val) { value = val; }
        }


        //@TODO: No tests for invalid shared components / destroyed shared component data
        //@TODO: No tests for if we leak shared data when last entity is destroyed...
        //@TODO: No tests for invalid shared component type?

        [Test]
        public void SetSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData), typeof(SharedData2));

            var group1 = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData1));
            var group2 = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData2));
            var group12 = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData2), typeof(SharedData1));

            Assert.AreEqual(0, group1.CalculateLength());
            Assert.AreEqual(0, group2.CalculateLength());
            Assert.AreEqual(0, group12.CalculateLength());

            var group1_filter_0 = group1.GetVariation(new SharedData1(0));
            var group1_filter_20 = group1.GetVariation(new SharedData1(20));
            Assert.AreEqual(0, group1_filter_0.CalculateLength());
            Assert.AreEqual(0, group1_filter_20.CalculateLength());

            Entity e1 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponent(e1, new EcsTestData(117));
            Entity e2 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponent(e2, new EcsTestData(243));

            Assert.AreEqual(2, group1_filter_0.CalculateLength());
            Assert.AreEqual(0, group1_filter_20.CalculateLength());
            Assert.AreEqual(117, group1_filter_0.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(243, group1_filter_0.GetComponentDataArray<EcsTestData>()[1].value);

            m_Manager.SetSharedComponent(e1, new SharedData1(20));

            Assert.AreEqual(1, group1_filter_0.CalculateLength());
            Assert.AreEqual(1, group1_filter_20.CalculateLength());
            Assert.AreEqual(117, group1_filter_20.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(243, group1_filter_0.GetComponentDataArray<EcsTestData>()[0].value);

            m_Manager.SetSharedComponent(e2, new SharedData1(20));

            Assert.AreEqual(0, group1_filter_0.CalculateLength());
            Assert.AreEqual(2, group1_filter_20.CalculateLength());
            Assert.AreEqual(117, group1_filter_20.GetComponentDataArray<EcsTestData>()[0].value);
            Assert.AreEqual(243, group1_filter_20.GetComponentDataArray<EcsTestData>()[1].value);


            group1.Dispose();
            group2.Dispose();
            group12.Dispose();
            group1_filter_0.Dispose();
            group1_filter_20.Dispose();
        }


        [Test]
        public void GetComponentArray()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData), typeof(SharedData2));

            const int entitiesPerValue = 5000;
            for (int i = 0; i < entitiesPerValue*8; ++i)
            {
                Entity e = m_Manager.CreateEntity((i % 2 == 0) ? archetype1 : archetype2);
                m_Manager.SetComponent(e, new EcsTestData(i));
                m_Manager.SetSharedComponent(e, new SharedData1(i%8));
            }

            var group = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData1));

            for (int sharedValue = 0; sharedValue < 8; ++sharedValue)
            {
                bool[] foundEntities = new bool[entitiesPerValue];
                var filteredGroup = group.GetVariation(new SharedData1(sharedValue));
                var componentArray = filteredGroup.GetComponentDataArray<EcsTestData>();
                Assert.AreEqual(entitiesPerValue, componentArray.Length);
                for (int i = 0; i < entitiesPerValue; ++i)
                {
                    int index = componentArray[i].value;
                    Assert.AreEqual(sharedValue, index % 8);
                    Assert.IsFalse(foundEntities[index/8]);
                    foundEntities[index/8] = true;
                }
                filteredGroup.Dispose();
            }

            group.Dispose();
        }

        [Test]
        public void GetAllUniqueSharedComponents()
        {
            var unique = new List<SharedData1>(0);
            m_Manager.GetAllUniqueSharedComponents(unique);

            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);

            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponent(e, new SharedData1(17));

            unique.Clear();
            m_Manager.GetAllUniqueSharedComponents(unique);

            Assert.AreEqual(2, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
            Assert.AreEqual(17, unique[1].value);

            m_Manager.SetSharedComponent(e, new SharedData1(34));

            unique.Clear();
            m_Manager.GetAllUniqueSharedComponents(unique);

            Assert.AreEqual(2, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
            Assert.AreEqual(34, unique[1].value);

            m_Manager.DestroyEntity(e);

            unique.Clear();
            m_Manager.GetAllUniqueSharedComponents(unique);

            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
        }

        [Test]
        public void GetSharedComponentData()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.AreEqual(0, m_Manager.GetSharedComponent<SharedData1>(e).value);

            m_Manager.SetSharedComponent(e, new SharedData1(17));

            Assert.AreEqual(17, m_Manager.GetSharedComponent<SharedData1>(e).value);
        }

        [Test]
        public void NonExistingSharedComponentDataThrows()
        {
            Entity e = m_Manager.CreateEntity(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() => { m_Manager.GetSharedComponent<SharedData1>(e); });
            Assert.Throws<ArgumentException>(() => { m_Manager.SetSharedComponent(e, new SharedData1()); });
        }

        [Test]
        public void AddSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));

            m_Manager.AddSharedComponent(e, new SharedData1(17));

            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponent<SharedData1>(e).value);

            m_Manager.AddSharedComponent(e, new SharedData2(34));
            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsTrue(m_Manager.HasComponent<SharedData2>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponent<SharedData1>(e).value);
            Assert.AreEqual(34, m_Manager.GetSharedComponent<SharedData2>(e).value);
        }

        [Test]
        public void RemoveSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            m_Manager.AddSharedComponent(e, new SharedData1(17));
            m_Manager.AddSharedComponent(e, new SharedData2(34));

            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsTrue(m_Manager.HasComponent<SharedData2>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponent<SharedData1>(e).value);
            Assert.AreEqual(34, m_Manager.GetSharedComponent<SharedData2>(e).value);

            m_Manager.RemoveSharedComponent<SharedData1>(e);
            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(e));

            m_Manager.RemoveSharedComponent<SharedData2>(e);
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));
        }

        private int oddTestValue = 34;
        private int evenTestValue = 17;

        private void AddEvenOddTestData()
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
                    m_Manager.AddSharedComponent(e,evenShared);
                }
                else
                {
                    m_Manager.AddSharedComponent(e,oddShared);
                }
            }
        } 

        private void ActionEvenOdd( Action<int, ComponentGroup> even, Action<int, ComponentGroup> odd )
        {
            var uniqueTypes = new List<SharedData1>(10);
            var maingroup = m_Manager.CreateComponentGroup(typeof(EcsTestData), typeof(SharedData1));
            maingroup.CompleteDependency();

            m_Manager.GetAllUniqueSharedComponents(uniqueTypes);

            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count;sharedIndex++)
            {
                var sharedData = uniqueTypes[sharedIndex];
                var group = maingroup.GetVariation(sharedData);
                int version = maingroup.GetVariationVersion(sharedData);

                if (sharedData.value == evenTestValue)
                {
                    even(version, group);
                }
                    
                if (sharedData.value == oddTestValue)
                {
                    odd(version, group);
                }

                group.Dispose();
            }

            maingroup.Dispose();
        }
        
        [Test]
        public void SharedComponentNoChangeVersionUnchanged()
        {
            AddEvenOddTestData();
            ActionEvenOdd( (version,group) => { Assert.AreEqual(version,1); }, (version,group) => { Assert.AreEqual(version,1); } );
        }
        
        private void testSourceEvenValues(int version, ComponentGroup group)
        {
            var testData = group.GetComponentDataArray<EcsTestData>();
                
            Assert.AreEqual(50,testData.Length);
                        
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(i*2,testData[i].value);
            }
        }
        
        private void testSourceOddValues(int version, ComponentGroup group)
        {
            var testData = group.GetComponentDataArray<EcsTestData>();
                
            Assert.AreEqual(50,testData.Length);
                        
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(1+(i*2),testData[i].value);
            }
        }
        
        [Test]
        public void SharedComponentNoChangeValuesUnchanged()
        {
            AddEvenOddTestData();
            ActionEvenOdd(testSourceEvenValues, testSourceOddValues);
        }
        
        private void ChangeGroupOrder(int version, ComponentGroup group)
        {
            var entityData = group.GetEntityArray();
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
            ActionEvenOdd( (version,group) => { Assert.AreEqual(version,1); }, (version,group) => { Assert.Greater(version,1); } );
        }
        
        [Test]
        public void SharedComponentChangeOddGroupOrderEvenValuesUnchanged()
        {
            AddEvenOddTestData();

            ActionEvenOdd((version, group) => { }, ChangeGroupOrder);
            ActionEvenOdd(testSourceEvenValues, (version, group) => { });
        }
        
        private void DestroyAllButOneEntityInGroup(int version, ComponentGroup group)
        {
            var entityData = group.GetEntityArray();
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
            ActionEvenOdd( (version,group) => { Assert.AreEqual(version,1); }, (version,group) => { Assert.Greater(version,1); } );
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