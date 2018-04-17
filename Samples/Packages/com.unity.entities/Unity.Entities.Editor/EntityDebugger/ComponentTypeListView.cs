
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.Entities.Editor
{
    public class ComponentTypeListView : TreeView
    {
        private List<ComponentType> types;
        private List<bool> typeSelections;

        private IComponentTypeQueryWindow window;

        public ComponentTypeListView(TreeViewState state, List<ComponentType> types, List<bool> typeSelections, IComponentTypeQueryWindow window) : base(state)
        {
            this.window = window;
            this.types = types;
            this.typeSelections = typeSelections;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root  = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
            if (types.Count == 0)
            {
                root.AddChild(new TreeViewItem { id = 1, displayName = "No types" });
            }
            else
            {
                for (var i = 0; i < types.Count; ++i)
                {
                    var displayName = (types[i].AccessModeType == ComponentType.AccessMode.Subtractive ? "-" : "") + types[i].GetManagedType().Name;
                    root.AddChild(new TreeViewItem {id = i, displayName = displayName});
                }
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);

            EditorGUI.BeginChangeCheck();
            typeSelections[args.item.id] = EditorGUI.Toggle(args.rowRect, typeSelections[args.item.id]);
            if (EditorGUI.EndChangeCheck())
            {
                window.ComponentFilterChanged();
            }
        }
    }
}
