using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.ECS;
using UnityEngine;

namespace UnityEditor.ECS
{
    public interface ISystemSelectionWindow
    {

        ScriptBehaviourManager SystemSelection { set; }

    }
    
    public class GroupedSystemListView : TreeView {

        Dictionary<Type, List<ScriptBehaviourManager>> managersByGroup = new Dictionary<Type, List<ScriptBehaviourManager>>();
        private List<ScriptBehaviourManager> floatingManagers = new List<ScriptBehaviourManager>();
        Dictionary<int, ScriptBehaviourManager> managersByID = new Dictionary<int, ScriptBehaviourManager>();

        private World world;

        private const float kToggleWidth = 22f;

        readonly ISystemSelectionWindow window;

        public static MultiColumnHeaderState GetHeaderState()
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = GUIContent.none,
                    contextMenuText = "Enabled",
                    headerTextAlignment = TextAlignment.Left,
                    canSort = false,
                    width = kToggleWidth,
                    minWidth = kToggleWidth,
                    maxWidth = kToggleWidth,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("System Name"),
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right,
                    canSort = true,
                    sortedAscending = true,
                    width = 100,
                    minWidth = 100,
                    maxWidth = 2000,
                    autoResize = true,
                    allowToggleVisibility = false
                }
            };
            
            return new MultiColumnHeaderState(columns);
        }

        public static TreeViewState GetStateForWorld(World world, ref List<TreeViewState> states,
            ref List<string> stateNames)
        {
            if (world == null)
                return new TreeViewState();

            if (states == null)
            {
                states = new List<TreeViewState>();
                stateNames = new List<string>();
            }
            var currentWorldName = world.GetType().Name;

            TreeViewState stateForCurrentWorld = null;
            for (var i = 0; i < states.Count; ++i)
            {
                if (stateNames[i] == currentWorldName)
                {
                    stateForCurrentWorld = states[i];
                    break;
                }
            }
            if (stateForCurrentWorld == null)
            {
                stateForCurrentWorld = new TreeViewState();
                states.Add(stateForCurrentWorld);
                stateNames.Add(currentWorldName);
            }
            return stateForCurrentWorld;
        }

        public GroupedSystemListView(TreeViewState state, MultiColumnHeader header, ISystemSelectionWindow window) : base(state, header)
        {
            this.window = window;
            Reload();
        }

        public void SetWorld(World world)
        {
            this.world = world;
            SetManagers();
        }
        
        void SetManagers()
        {
            Dictionary<Type, ScriptBehaviourUpdateOrder.ScriptBehaviourGroup> allGroups;
            Dictionary<Type, ScriptBehaviourUpdateOrder.DependantBehavior> dependencies;
            ScriptBehaviourUpdateOrder.CollectGroups(world.BehaviourManagers, out allGroups, out dependencies);
            
            managersByGroup.Clear();
            managersByID.Clear();
            floatingManagers.Clear();
            
            foreach (var manager in world.BehaviourManagers)
            {
                var hasGroup = false;
                foreach (var attributeData in manager.GetType().GetCustomAttributesData())
                {
                    if (attributeData.AttributeType == typeof(UpdateInGroupAttribute))
                    {
                        var groupType = (Type) attributeData.ConstructorArguments[0].Value;
                        if (!managersByGroup.ContainsKey(groupType))
                            managersByGroup[groupType] = new List<ScriptBehaviourManager>{manager};
                        else
                            managersByGroup[groupType].Add(manager);
                        hasGroup = true;
                        break;
                    }
                }

                if (!hasGroup)
                {
                    floatingManagers.Add(manager);
                }
            }
            foreach (var managerSet in managersByGroup.Values)
            {
                managerSet.Sort((x, y) => string.CompareOrdinal(x.GetType().Name, y.GetType().Name));
            }
            Reload();
            SelectionChanged(GetSelection());
        }

        protected override TreeViewItem BuildRoot()
        {
            var currentID = 0;
            var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
            if (managersByGroup.Count == 0 && floatingManagers.Count == 0)
            {
                root.AddChild(new TreeViewItem { id = currentID++, displayName = "No ComponentSystems Loaded"});
            }
            else
            {

                foreach (var manager in floatingManagers)
                {
                    managersByID.Add(currentID, manager);
                    var managerItem = new TreeViewItem { id = currentID++, displayName = manager.GetType().Name.ToString() };
                    root.AddChild(managerItem);
                }
                foreach (var group in (from g in managersByGroup.Keys orderby g.Name select g))
                {
                    var groupItem = new TreeViewItem { id = currentID++, displayName = group.Name };
                    root.AddChild(groupItem);
                    foreach (var manager in managersByGroup[group])
                    {
                        managersByID.Add(currentID, manager);
                        var managerItem = new TreeViewItem { id = currentID++, displayName = manager.GetType().Name.ToString() };
                        groupItem.AddChild(managerItem);
                    }
                }
                SetupDepthsFromParentsAndChildren(root);
            }
            return root;
        }

        protected override void RowGUI (RowGUIArgs args)
        {
            if (args.item.depth == -1)
                return;
            var item = args.item;
            var manager = managersByID[item.id];

            if (manager != null)
            {
                var toggleRect = args.GetCellRect(0);
                toggleRect.xMin = toggleRect.xMin + 4f;
                manager.Enabled = GUI.Toggle(toggleRect, manager.Enabled, GUIContent.none);
            }

            var nameRect = args.GetCellRect(1);
            GUI.Label(nameRect, item.displayName);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count > 0 && managersByID.ContainsKey(selectedIds[0]))
            {
                window.SystemSelection = managersByID[selectedIds[0]];
            }
            else
            {
                window.SystemSelection = null;
            }
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

    }
}
