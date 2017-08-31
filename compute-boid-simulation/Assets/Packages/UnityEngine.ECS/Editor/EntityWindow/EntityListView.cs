using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;
using UnityEngine.Jobs;
using System.Linq;

namespace UnityEngine.ECS
{
    public class EntityListView : TreeView {

        TupleSystem currentSystem;

        EntityWindow window;

        public EntityListView(TreeViewState state, EntityWindow window) : base(state)
        {
            this.window = window;
            Reload();
        }

        public void SetSelection(TupleSystem system)
        {
            currentSystem = system;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root  = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
            if (currentSystem == null)
            {
                root.AddChild(new TreeViewItem { id = 0, displayName = "No Tuple selected"});
            }
            else
            {
                for (var entityIndex = 0; entityIndex < currentSystem.GetEntityArray().Length; ++entityIndex)
                {
                    root.AddChild(new TreeViewItem { id = entityIndex, displayName = string.Format("Entity {0}", entityIndex)});
                }
                SetupDepthsFromParentsAndChildren(root);
            }
            return root;
        }

        // override protected void RowGUI(RowGUIArgs args)
        // {
        //     base.RowGUI(args);
        //     GUI.Label(args.rowRect, tuplesById[args.item.id].GetEntityArray().Length.ToString(), rightAlignedLabel);
        // }

    }
}