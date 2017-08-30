using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;

namespace UnityEngine.ECS
{
    public class SystemListView : TreeView {
        
        Dictionary<string, List<ComponentSystem>> managersByNamespace;
        Dictionary<int, ComponentSystem> managersByID;

        EntityWindow window;

        public SystemListView(TreeViewState state, EntityWindow window) : base(state)
        {
            this.window = window;
            Reload();
        }

        public void SetManagers(ComponentSystem[] managers)
        {
            managersByNamespace = new Dictionary<string, List<ComponentSystem>>();
            managersByID = new Dictionary<int, ComponentSystem>();
            foreach (var manager in managers)
            {
                var ns = manager.GetType().Namespace ?? "global";
                if (!managersByNamespace.ContainsKey(ns))
                    managersByNamespace[ns] = new List<ComponentSystem>{manager};
                else
                    managersByNamespace[ns].Add(manager);
            }
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var currentID = 0;
            var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
            if (managersByNamespace == null)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No Managers Loaded"});
            }
            else
            {
                foreach (var ns in managersByNamespace.Keys)
                {
                    var nsItem = new TreeViewItem { id = currentID++, displayName = ns };
                    root.AddChild(nsItem);
                    foreach (var manager in managersByNamespace[ns])
                    {
                        managersByID.Add(currentID, manager);
                        var managerItem = new TreeViewItem { id = currentID++, displayName = manager.GetType().Name.ToString() };
                        nsItem.AddChild(managerItem);
                    }
                }
                SetupDepthsFromParentsAndChildren(root);
            }
            return root;
        }

        override protected void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count > 0 && managersByID.ContainsKey(selectedIds[0]))
            {
                window.CurrentSelection = managersByID[selectedIds[0]];
            }
            else
            {
                window.CurrentSelection = null;
            }
        }

        override protected bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

    }
}