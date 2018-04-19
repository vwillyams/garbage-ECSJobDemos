
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.Entities.Editor
{
    public interface IComponentTypeFilterWindow : IWorldSelectionWindow
    {
        void SetFilter(ComponentGroup group);
    }
    
    public class ComponentTypeFilterUI : IComponentTypeQueryWindow
    {
        private readonly IComponentTypeFilterWindow window;

        private readonly List<bool> selectedFilterTypes = new List<bool>();
        private readonly List<ComponentType> filterTypes = new List<ComponentType>();

        private readonly List<ComponentGroup> componentGroups = new List<ComponentGroup>();

        public ComponentTypeFilterUI(IComponentTypeFilterWindow window)
        {
            this.window = window;
        }
        
        public void GetTypes()
        {
            if (selectedFilterTypes.Count != 2* (TypeManager.TypesCount - 2)) // First two entries are not ComponentTypes
            {
                filterTypes.Clear();
                selectedFilterTypes.Clear();
                var requiredTypes = new List<ComponentType>();
                var subtractiveTypes = new List<ComponentType>();
                filterTypes.Capacity = TypeManager.TypesCount;
                selectedFilterTypes.Capacity = TypeManager.TypesCount;
                foreach (var type in TypeManager.AllTypes())
                {
                    if (type.Type == typeof(Entity)) continue;
                    var typeIndex = TypeManager.GetTypeIndex(type.Type);
                    var componentType = ComponentType.FromTypeIndex(typeIndex);
                    if (componentType.GetManagedType() == null) continue;
                    requiredTypes.Add(componentType);
                    componentType.AccessModeType = ComponentType.AccessMode.Subtractive;
                    subtractiveTypes.Add(componentType);
                    selectedFilterTypes.Add(false);
                    selectedFilterTypes.Add(false);
                }

                filterTypes.AddRange(requiredTypes);
                filterTypes.AddRange(subtractiveTypes);
            }
        }

        public void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter: ");
            var filterCount = 0;
            for (var i = 0; i < selectedFilterTypes.Count; ++i)
            {
                if (selectedFilterTypes[i])
                {
                    ++filterCount;
                    var style = filterTypes[i].AccessModeType == ComponentType.AccessMode.Subtractive ? EntityDebuggerStyles.ComponentSubtractive : EntityDebuggerStyles.ComponentRequired;
                    GUILayout.Label(filterTypes[i].GetManagedType().Name, style);
                }
            }
            if (filterCount == 0)
                GUILayout.Label("none");
            if (GUILayout.Button("Edit"))
            {
                ComponentTypeChooser.Open(GUIUtility.GUIToScreenPoint(GUILayoutUtility.GetLastRect().position), filterTypes, selectedFilterTypes, this);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private ComponentGroup GetComponentGroup(ComponentType[] components)
        {
            foreach (var existingGroup in componentGroups)
            {
                if (existingGroup.CompareComponents(components))
                    return existingGroup;
            }

            var group = window.WorldSelection.GetExistingManager<EntityManager>()
                .CreateComponentGroup(components);
            componentGroups.Add(group);

            return group;
        }

        public void ComponentFilterChanged()
        {
            var selectedTypes = new List<ComponentType>();
            for (var i = 0; i < selectedFilterTypes.Count; ++i)
            {
                if (selectedFilterTypes[i])
                    selectedTypes.Add(filterTypes[i]);
            }
            var group = GetComponentGroup(selectedTypes.ToArray());
            window.SetFilter(group);
        }
    }
}
