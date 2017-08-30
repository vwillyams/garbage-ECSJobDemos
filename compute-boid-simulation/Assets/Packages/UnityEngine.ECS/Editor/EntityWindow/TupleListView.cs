using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;
using UnityEngine.Jobs;
using System.Linq;

namespace UnityEngine.ECS
{
    public class TupleListView : TreeView {
        
        Dictionary<int, TupleSystem> tuplesById;

        ComponentSystem currentSystem;

        EntityWindow window;

        GUIStyle rightAlignedLabel {
            get {
                if (m_RightAlignedLabel == null)
                {
                    m_RightAlignedLabel = new GUIStyle("PR Label");
                    m_RightAlignedLabel.alignment = TextAnchor.MiddleRight;
                }
                return m_RightAlignedLabel;
            }
        }
        GUIStyle m_RightAlignedLabel;

        public TupleListView(TreeViewState state, EntityWindow window) : base(state)
        {
            this.window = window;
            Reload();
        }

        public void SetSelection(ComponentSystem system)
        {
            currentSystem = system;
            tuplesById = new Dictionary<int, TupleSystem>();
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
            else
            {
                var tupleIndex = 0;
                foreach (var tupleSystem in currentSystem.Tuples)
                {
                    tuplesById.Add(currentID, tupleSystem);
                    var types = tupleSystem.EntityGroup.Types;
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
            GUI.Label(args.rowRect, tuplesById[args.item.id].GetEntityArray().Length.ToString(), rightAlignedLabel);
        }

        override protected void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count > 0 && tuplesById.ContainsKey(selectedIds[0]))
            {
                window.CurrentTupleSelection = tuplesById[selectedIds[0]];
            }
            else
            {
                window.CurrentTupleSelection = null;
            }
        }

        override protected bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

    }
}