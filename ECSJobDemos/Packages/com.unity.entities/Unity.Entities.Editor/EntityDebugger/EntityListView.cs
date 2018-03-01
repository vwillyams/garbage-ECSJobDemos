using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace UnityEditor.ECS
{
    public interface IEntitySelectionWindow
    {
        Entity EntitySelection { set; }
        World WorldSelection { get; }
    }
    
    public class EntityListView : TreeView {
        private readonly Dictionary<int, ComponentGroup> componentGroupsById = new Dictionary<int, ComponentGroup>();
        private readonly Dictionary<int, Entity> entitiesById = new Dictionary<int, Entity>();

        public ComponentSystemBase SelectedSystem
        {
            get { return selectedSystem; }
            set
            {
                if (selectedSystem != value)
                {
                    selectedSystem = value;
                    Reload();
                }
            }
        }
        private ComponentSystemBase selectedSystem;

        IEntitySelectionWindow window;

        public EntityListView(TreeViewState state, IEntitySelectionWindow window, ComponentSystemBase system) : base(state)
        {
            this.window = window;
            selectedSystem = system;
            Reload();
        }
        
        public void RefreshData()
        {
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var currentID = 0;
            var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
            componentGroupsById.Clear();
            entitiesById.Clear();
            if (window?.WorldSelection == null)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No world selected"});
            }
            else if (SelectedSystem == null)
            {
                var entityArray = window.WorldSelection.GetExistingManager<EntityManager>().GetAllEntities(Allocator.Temp);
                for (var i = 0; i < entityArray.Length; ++i)
                {
                    entitiesById.Add(currentID, entityArray[i]);
                    var entityItem = new TreeViewItem { id = currentID++, displayName = $"Entity {entityArray[i].Index.ToString()}" };
                    root.AddChild(entityItem);
                }
                entityArray.Dispose();
                SetupDepthsFromParentsAndChildren(root);
            }
            else if (SelectedSystem.ComponentGroups.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No Component Groups in Manager"});
            }
            else
            {
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
                        entitiesById.Add(currentID, entityArray[i]);
                        var entityItem = new TreeViewItem { id = currentID++, displayName = $"Entity {entityArray[i].Index.ToString()}" };
                        groupItem.AddChild(entityItem);
                    }
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

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);
            if (!componentGroupsById.ContainsKey(args.item.id))
                return;
            var countString = componentGroupsById[args.item.id].CalculateLength().ToString();
            DefaultGUI.LabelRightAligned(args.rowRect, countString, args.selected, args.focused);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (window == null)
                return;
            if (selectedIds.Count > 0 && entitiesById.ContainsKey(selectedIds[0]))
            {
                window.EntitySelection = entitiesById[selectedIds[0]];
            }
            else
            {
                window.EntitySelection = Entity.Null;
            }
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }
    }
}
