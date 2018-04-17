
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
        private List<ComponentMatchMode> typeModes;

        private IComponentTypeQueryWindow window;

        public ComponentTypeListView(TreeViewState state, List<ComponentType> types, List<ComponentMatchMode> typeModes, IComponentTypeQueryWindow window) : base(state)
        {
            this.window = window;
            this.types = types;
            this.typeModes = typeModes;
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
                    if (types[i].GetManagedType() == null) continue;
                    root.AddChild(new TreeViewItem {id = i, displayName = types[i].GetManagedType().Name});
                }
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);

            EditorGUI.BeginChangeCheck();
            typeModes[args.item.id] = MatchModeToggle(args.rowRect, typeModes[args.item.id]);
            if (EditorGUI.EndChangeCheck())
            {
                window.ComponentFilterChanged();
            }
        }

        static ComponentMatchMode MatchModeToggle(Rect rect, ComponentMatchMode value)
        {

            if (value == ComponentMatchMode.Ignore)
            {
                var newValue = EditorGUI.Toggle(rect, false);
                if (newValue)
                {
                    return ComponentMatchMode.Require;
                }
            }
            else if (value == ComponentMatchMode.Require)
            {
                var newValue = EditorGUI.Toggle(rect, true);
                if (!newValue)
                {
                    return ComponentMatchMode.Subtract;
                }
            }
            else
            {
                EditorGUI.showMixedValue = true;
                var newValue = EditorGUI.Toggle(rect, true);
                EditorGUI.showMixedValue = false;
                if (newValue)
                {
                    return ComponentMatchMode.Ignore;
                }
            }

            return value;
        }
    }
}
