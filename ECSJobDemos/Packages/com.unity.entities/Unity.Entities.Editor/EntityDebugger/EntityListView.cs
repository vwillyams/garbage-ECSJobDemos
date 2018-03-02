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

        private static TreeViewState GetStateForSystem(ComponentSystemBase system, List<TreeViewState> states, List<string> stateNames)
        {
            if (system == null)
                return new TreeViewState();
            
            var currentSystemName = system.GetType().FullName;

            var stateForCurrentSystem = states.Where((t, i) => stateNames[i] == currentSystemName).FirstOrDefault();
            if (stateForCurrentSystem != null)
                return stateForCurrentSystem;
            
            stateForCurrentSystem = new TreeViewState();
            states.Add(stateForCurrentSystem);
            stateNames.Add(currentSystemName);
            return stateForCurrentSystem;
        }

        public static EntityListView CreateList([CanBeNull] ComponentSystemBase system, [NotNull] List<TreeViewState> states, [NotNull] List<string> stateNames,
            IEntitySelectionWindow window)
        {
            var state = GetStateForSystem(system, states, stateNames);
            return new EntityListView(state, system, window);
        }

        public EntityListView(TreeViewState state, ComponentSystemBase system, IEntitySelectionWindow window) : base(state)
        {
            this.window = window;
            selectedSystem = system;
            Reload();
            SelectionChanged(GetSelection());
        }
        
        public void RefreshData()
        {
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            componentGroupsById.Clear();
            entitiesById.Clear();
            var managerId = -1;
            var root  = new TreeViewItem { id = managerId--, depth = -1, displayName = "Root" };
            if (window?.WorldSelection == null)
            {
                root.AddChild(new TreeViewItem { id = managerId, displayName = "No world selected"});
            }
            else if (SelectedSystem == null)
            {
                var entityArray = window.WorldSelection.GetExistingManager<EntityManager>().GetAllEntities(Allocator.Temp);
                for (var i = 0; i < entityArray.Length; ++i)
                {
                    var entity = entityArray[i];
                    entitiesById.Add(entity.Index, entity);
                    var entityItem = new TreeViewItem { id = entity.Index, displayName = $"Entity {entity.Index.ToString()}" };
                    root.AddChild(entityItem);
                }
                entityArray.Dispose();
                SetupDepthsFromParentsAndChildren(root);
            }
            else if (SelectedSystem.ComponentGroups.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = managerId, displayName = "No Component Groups in Manager"});
            }
            else
            {
                foreach (var group in SelectedSystem.ComponentGroups)
                {
                    componentGroupsById.Add(managerId, group);
                    var types = group.Types;
                    var groupName = string.Join(", ", (from x in types select x.Name).ToArray());

                    var groupItem = new TreeViewItem { id = managerId--, displayName = groupName };
                    root.AddChild(groupItem);
                    var entityArray = group.GetEntityArray();
                    for (var i = 0; i < entityArray.Length; ++i)
                    {
                        var entity = entityArray[i];
                        entitiesById.Add(entity.Index, entity);
                        var entityItem = new TreeViewItem { id = entity.Index, displayName = $"Entity {entity.Index.ToString()}" };
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

        public void SelectNothing()
        {
            SetSelection(new List<int>());
        }

        public void SetEntitySelection(Entity entitySelection)
        {
            if (entitySelection != Entity.Null && window.WorldSelection.GetExistingManager<EntityManager>().Exists(entitySelection))
                SetSelection(new List<int>{entitySelection.Index});
        }
    }
}
