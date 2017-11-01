using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;
using Unity.Jobs;
using System.Linq;

namespace UnityEngine.ECS
{
    public class TupleListView : TreeView {
        
        Dictionary<int, ComponentGroup> componentGroupsById;

        ComponentSystem currentSystem;

        EntityWindow window;

        public TupleListView(TreeViewState state, EntityWindow window) : base(state)
        {
            this.window = window;
            Reload();
        }

        public void SetSelection(ComponentSystem system)
        {
            currentSystem = system;
            componentGroupsById = new Dictionary<int, ComponentGroup>();
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var currentID = 0;
            var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
            if (currentSystem == null)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No Manager selected"});
            }
            else if (currentSystem.ComponentGroups.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No Component Groups in Manager"});
            }
            else
            {
                var tupleIndex = 0;
                foreach (var group in currentSystem.ComponentGroups)
                {
                    componentGroupsById.Add(currentID, group);
                    var types = group.Types;
                    var tupleName = string.Join(", ", (from x in types select x.Name).ToArray());

                    var tupleItem = new TreeViewItem { id = currentID++, displayName = string.Format("({1}):", tupleIndex, tupleName) };
                    root.AddChild(tupleItem);
                    ++tupleIndex;
                }
                SetupDepthsFromParentsAndChildren(root);
            }
            return root;
        }

        override protected void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);
            if (!componentGroupsById.ContainsKey(args.item.id))
                return;
            var countString = componentGroupsById[args.item.id].GetEntityArray().Length.ToString();
            DefaultGUI.LabelRightAligned(args.rowRect, countString, args.selected, args.focused);
        }

        override protected void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count > 0 && componentGroupsById.ContainsKey(selectedIds[0]))
            {
                window.CurrentComponentGroupSelection = componentGroupsById[selectedIds[0]];
            }
            else
            {
                window.CurrentComponentGroupSelection = null;
            }
        }

        override protected bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

    }
}
