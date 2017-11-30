using UnityEngine;
using UnityEngine.ECS;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;

namespace UnityEditor.ECS
{
    public class SystemListView : TreeView {
        
        Dictionary<string, List<ComponentSystemBase>> managersByNamespace;
        Dictionary<int, ComponentSystemBase> managersByID;

        EntityWindow window;

        public SystemListView(TreeViewState state, EntityWindow window) : base(state)
        {
            this.window = window;
            Reload();
        }

        public void SetManagers(ComponentSystemBase[] managers)
        {
            managersByNamespace = new Dictionary<string, List<ComponentSystemBase>>();
            managersByID = new Dictionary<int, ComponentSystemBase>();
            foreach (var manager in managers)
            {
                var ns = manager.GetType().Namespace ?? "global";
                if (!managersByNamespace.ContainsKey(ns))
                    managersByNamespace[ns] = new List<ComponentSystemBase>{manager};
                else
                    managersByNamespace[ns].Add(manager);
            }
            Reload();
            SelectionChanged(GetSelection());
        }

        protected override TreeViewItem BuildRoot()
        {
            var currentID = 0;
            var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
            if (managersByNamespace == null || managersByNamespace.Count == 0)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No ComponentSystems Loaded"});
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
                window.CurrentSystemSelection = managersByID[selectedIds[0]];
            }
            else
            {
                window.CurrentSystemSelection = null;
            }
        }

        override protected bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

    }
}
