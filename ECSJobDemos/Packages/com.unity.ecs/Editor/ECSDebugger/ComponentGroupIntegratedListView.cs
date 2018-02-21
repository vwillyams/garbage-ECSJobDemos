﻿using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.ECS;
using UnityEngine;

namespace UnityEditor.ECS
{
    public interface IEntitySelectionWindow
    {
        Entity EntitySelection { set; }
        World WorldSelection { get; }
    }
    
    public class ComponentGroupIntegratedListView : TreeView {

        Dictionary<int, ComponentGroup> componentGroupsById;

        public ComponentSystemBase SelectedSystem
        {
            get { return selectedSystem; }
            set
            {
                selectedSystem = value;
                Reload();
            }
        }
        private ComponentSystemBase selectedSystem;

        IEntitySelectionWindow window;

//        public static TreeViewState GetStateForSystem(ComponentSystemBase system, ref List<TreeViewState> states, ref List<string> stateNames)
//        {
//            if (system == null)
//                return new TreeViewState();
//
//            if (states == null)
//            {
//                states = new List<TreeViewState>();
//                stateNames = new List<string>();
//            }
//            var currentSystemName = system.GetType().Name.ToString();
//
//            TreeViewState stateForCurrentSystem = null;
//            for (var i = 0; i < states.Count; ++i)
//            {
//                if (stateNames[i] == currentSystemName)
//                {
//                    stateForCurrentSystem = states[i];
//                    break;
//                }
//            }
//            if (stateForCurrentSystem == null)
//            {
//                stateForCurrentSystem = new TreeViewState();
//                states.Add(stateForCurrentSystem);
//                stateNames.Add(currentSystemName);
//            }
//            return stateForCurrentSystem;
//        }

        public ComponentGroupIntegratedListView(TreeViewState state, IEntitySelectionWindow window, ComponentSystemBase system) : base(state)
        {
            this.window = window;
            selectedSystem = system;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            componentGroupsById = new Dictionary<int, ComponentGroup>();
            var currentID = 0;
            var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
            if (SelectedSystem == null)
            {
                var groupItem = new TreeViewItem { id = currentID++, displayName = "All Entities" };
                root.AddChild(groupItem);
//                var entityArray = window.WorldSelection.GetExistingManager<EntityManager>().;
//                for (var i = 0; i < entityArray.Length; ++i)
//                {
//                    var entityItem = new TreeViewItem { id = currentID++, displayName = $"Entity {entityArray[i].Index.ToString()}" };
//                    groupItem.AddChild(entityItem);
//                }
                SetupDepthsFromParentsAndChildren(root);
            }
            else if (SelectedSystem.ComponentGroups.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No Component Groups in Manager"});
            }
            else
            {
                var groupIndex = 0;
                foreach (var group in SelectedSystem.ComponentGroups)
                {
                    componentGroupsById.Add(currentID, group);
                    var types = group.Types;
                    var groupName = string.Join(", ", (from x in types select x.Name).ToArray());

                    var groupItem = new TreeViewItem { id = currentID++, displayName = groupName };
                    root.AddChild(groupItem);
                    var entityArray = group.GetEntityArray();
                    for (var i = 0; i < entityArray.Length; ++i)
                    {
                        var entityItem = new TreeViewItem { id = currentID++, displayName = $"Entity {entityArray[i].Index.ToString()}" };
                        groupItem.AddChild(entityItem);
                    }
                    ++groupIndex;
                }
                SetupDepthsFromParentsAndChildren(root);
            }
            return root;
        }

        public override void OnGUI(Rect rect)
        {
            if (window?.WorldSelection?.GetExistingManager<EntityManager>()?.IsCreated == true)
                base.OnGUI(rect);
        }

        override protected void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);
            if (!componentGroupsById.ContainsKey(args.item.id))
                return;
            var countString = componentGroupsById[args.item.id].CalculateLength().ToString();
            DefaultGUI.LabelRightAligned(args.rowRect, countString, args.selected, args.focused);
        }

//        override protected void SelectionChanged(IList<int> selectedIds)
//        {
//            if (selectedIds.Count > 0 && componentGroupsById.ContainsKey(selectedIds[0]))
//            {
//                window.EntitySelection = componentGroupsById[selectedIds[0]];
//            }
//            else
//            {
//                window.EntitySelection = null;
//            }
//        }

        override protected bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

    }
}
