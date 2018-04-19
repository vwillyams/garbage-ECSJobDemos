using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities.Tests;
using UnityEditor.IMGUI.Controls;

namespace Unity.Entities.Editor.Tests
{
    public class ListViewTests : ECSTestsFixture
    {
        public static void SetEntitySelection(Entity s, bool updateList)
        {
        }

        public World GetWorldSelection()
        {
            return World.Active;
        }

        public static void SetComponentGroupSelection(ComponentGroup group, bool updateList, bool propagate)
        {
        }

        public static void SetSystemSelection(ScriptBehaviourManager system, bool updateList, bool propagate)
        {
        }

        [Test]
        public void EntityListView_CanSetNullGroup()
        {
            var listView = new EntityListView(new TreeViewState(), null, SetEntitySelection, GetWorldSelection);
            
            Assert.DoesNotThrow( () => listView.SelectedComponentGroup = null );
        }

        [Test]
        public void ComponentGroupListView_CanSetNullSystem()
        {
            var listView = new ComponentGroupListView(new TreeViewState(), EmptySystem, SetComponentGroupSelection, GetWorldSelection);

            Assert.DoesNotThrow(() => listView.SelectedSystem = null);
        }

        [Test]
        public void SystemListView_CanCreateWithNullWorld()
        {
            SystemListView listView;
            var states = new List<TreeViewState>();
            var stateNames = new List<string>();
            Assert.DoesNotThrow(() =>
            {
                listView = SystemListView.CreateList(states, stateNames, SetSystemSelection, GetWorldSelection);
                listView.Reload();
            });
        }
        
    }
}
