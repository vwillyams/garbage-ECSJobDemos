using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.ECS;

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

        readonly ISystemSelectionWindow window;

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
            var currentWorldName = world.GetType().Name.ToString();

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

        public GroupedSystemListView(TreeViewState state, ISystemSelectionWindow window) : base(state)
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
