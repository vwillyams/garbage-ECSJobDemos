using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Editor
{
    public class EntityDebuggerTests : ECSTestsFixture
    {

        private EntityDebugger m_Window;
        private ComponentSystem m_System;
        private ComponentGroup m_ComponentGroup;
        private Entity m_Entity;
        
        class SingleGroupSystem : ComponentSystem
        {
            struct Group
            {
                private int Length;
                private ComponentDataArray<EcsTestData> testDatas;
            }

            [Inject] private Group entities;
            
            protected override void OnUpdate()
            {
                throw new NotImplementedException();
            }
        }

        private static void CloseAllDebuggers()
        {
            var windows = Resources.FindObjectsOfTypeAll<EntityDebugger>();
            foreach (var window in windows)
                window.Close();
        }

        public override void Setup()
        {
            base.Setup();

            CloseAllDebuggers();
            
            m_Window = EditorWindow.GetWindow<EntityDebugger>();

            m_System = World.Active.GetOrCreateManager<SingleGroupSystem>();

            m_ComponentGroup = m_System.ComponentGroups[0];

            m_Entity = m_Manager.CreateEntity(typeof(EcsTestData));
        }

        public override void TearDown()
        {
            CloseAllDebuggers();
            
            base.TearDown();
        }

        [Test]
        public void EntityDebugger_SetAllSelections()
        {
            
            EntityDebugger.SetAllSelections(World.Active, m_System, m_ComponentGroup, m_Entity);
            
            Assert.AreEqual(World.Active, m_Window.WorldSelection);
            Assert.AreEqual(m_System, m_Window.SystemSelection);
            Assert.AreEqual(m_ComponentGroup, m_Window.ComponentGroupSelection);
            Assert.AreEqual(m_Entity, m_Window.EntitySelection);
        }

        [Test]
        public void EntityDebugger_RememberSelections()
        {
            
            EntityDebugger.SetAllSelections(World.Active, m_System, m_ComponentGroup, m_Entity);
            
            m_Window.SetWorldSelection(null, true);
            
            m_Window.SetWorldSelection(World.Active, true);
            
            Assert.AreEqual(World.Active, m_Window.WorldSelection);
            Assert.AreEqual(m_System, m_Window.SystemSelection);
            Assert.AreEqual(m_ComponentGroup, m_Window.ComponentGroupSelection);
            Assert.AreEqual(m_Entity, m_Window.EntitySelection);
        }
        
    }
}
