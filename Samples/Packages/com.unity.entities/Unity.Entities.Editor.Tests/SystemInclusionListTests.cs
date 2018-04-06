﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities.Tests;

namespace Unity.Entities.Editor
{
    public class SystemInclusionListTests : ECSTestsFixture
    {

        class RegularSystem : ComponentSystem
        {
            struct Entities
            {
                public int Length;
                public ComponentDataArray<EcsTestData> tests;
            }

            [Inject] private Entities entities;
            
            protected override void OnUpdate()
            {
                throw new NotImplementedException();
            }
        }

        class SubtractiveSystem : ComponentSystem
        {
            struct Entities
            {
                public int Length;
                public ComponentDataArray<EcsTestData> tests;
                public SubtractiveComponent<EcsTestData2> noTest2;
            }

            [Inject] private Entities entities;
            
            protected override void OnUpdate()
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void SystemInclusionList_MatchesComponents()
        {
            var system = World.Active.GetOrCreateManager<RegularSystem>();
            
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var matchList = new List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>>();
            
            SystemInclusionList.CollectMatches(m_Manager, entity, matchList);
            
            Assert.AreEqual(1, matchList.Count);
            Assert.AreEqual(system, matchList[0].Item1);
            Assert.AreEqual(system.ComponentGroups[0], matchList[0].Item2[0]);
        }

        [Test]
        public void SystemInclusionList_IgnoresSubtractedComponents()
        {
            var system = World.Active.GetOrCreateManager<SubtractiveSystem>();
            
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var matchList = new List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>>();
            
            SystemInclusionList.CollectMatches(m_Manager, entity, matchList);
            
            Assert.AreEqual(0, matchList.Count);
        }
        
    }
}
