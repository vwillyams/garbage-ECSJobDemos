using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.Entities.Editor
{
    public class ListViewTests : ECSTestsFixture
    {

        class FakeWindow : IEntitySelectionWindow, IWorldSelectionWindow, IComponentGroupSelectionWindow, ISystemSelectionWindow
        {
            public Entity EntitySelection { get; private set; }

            public void SetEntitySelection(Entity s, bool updateList)
            {
                EntitySelection = s;
            }

            public World WorldSelection { get; set; }
            public ComponentGroup ComponentGroupSelection { get; private set; }

            public void SetComponentGroupSelection(ComponentGroup group, bool updateList, bool propagate)
            {
                ComponentGroupSelection = group;
            }
            public ScriptBehaviourManager SystemSelection { get; private set; }

            public void SetSystemSelection(ScriptBehaviourManager system, bool updateList, bool propagate)
            {
                SystemSelection = system;
            }
        }

        [Test]
        public void EntityListView_CanSetNullGroup()
        {

            var listView = new EntityListView(new TreeViewState(), null, new FakeWindow());
            
            Assert.DoesNotThrow( () => listView.SelectedComponentGroup = null );
        }
        
        [Test]
        public void EntityListView_CanCreateWithNullWindow()
        {
            EntityListView listView;
            
            Assert.DoesNotThrow( () =>
            {
                listView = new EntityListView(new TreeViewState(), null, null);
                listView.Reload();
            });
        }

        [Test]
        public void ComponentGroupListView_CanSetNullSystem()
        {
            var listView = new ComponentGroupListView(new TreeViewState(), EmptySystem, new FakeWindow());

            Assert.DoesNotThrow(() => listView.SelectedSystem = null);
        }
        
        [Test]
        public void ComponentGroupListView_CanCreateWithNullWindow()
        {
            ComponentGroupListView listView;
            
            Assert.DoesNotThrow( () =>
            {
                listView = new ComponentGroupListView(new TreeViewState(), null, null);
                listView.Reload();
            });
        }

        [Test]
        public void SystemListView_CanCreateWithNullWorld()
        {
            SystemListView listView;
            var states = new List<TreeViewState>();
            var stateNames = new List<string>();
            Assert.DoesNotThrow(() =>
            {
                listView = SystemListView.CreateList(states, stateNames, new FakeWindow());
                listView.Reload();
            });
        }
        
    }
}
