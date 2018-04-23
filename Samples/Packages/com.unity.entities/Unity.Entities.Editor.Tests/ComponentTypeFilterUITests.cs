using System;
using NUnit.Framework;
using Unity.Entities.Tests;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor.Tests
{
    public class ComponentTypeFilterUITests : ECSTestsFixture
    {

        public void SetFilterDummy(ComponentGroup group)
        {
            
        }

        private World WorldSelectionGetter()
        {
            return World.Active;
        }
        
        [Test]
        public void EntityDebugger_SetAllEntitiesFilter()
        {
            var filterUI = new ComponentTypeFilterUI(SetFilterDummy, WorldSelectionGetter);
            Assert.IsFalse(filterUI.TypeListValid());
            filterUI.GetTypes();
            Assert.IsTrue(filterUI.TypeListValid());
        }
    }
}
