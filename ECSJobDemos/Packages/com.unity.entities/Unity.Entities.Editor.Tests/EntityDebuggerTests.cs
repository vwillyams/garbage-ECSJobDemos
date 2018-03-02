using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.ECS.Tests;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.ECS
{
    public class EntityDebuggerTests : ECSTestsFixture
    {

        class FakeWindow : IEntitySelectionWindow
        {
            public Entity EntitySelection { get; set; }
            public World WorldSelection { get; }
        }
        
        [Test]
        public void ComponentGroupIntegratedListView_CanSetNullSystem()
        {

            var listView = new EntityListView(new TreeViewState(), EmptySystem, new FakeWindow());
            
            Assert.DoesNotThrow( () => listView.SelectedSystem = null );
        }
        
        [Test]
        public void ComponentGroupIntegratedListView_CanCreateWithNullWindow()
        {
            EntityListView listView;
            
            Assert.DoesNotThrow( () =>
            {
                listView = new EntityListView(new TreeViewState(), EmptySystem, null);
                listView.SelectedSystem = null;
            });
        }
        
    }
}
