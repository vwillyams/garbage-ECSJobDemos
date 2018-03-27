using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    [CustomEditor(typeof(EntitySelectionProxy))]
    public class EntitySelectionProxyEditor : UnityEditor.Editor
    {
        private EntityIMGUIVisitor visitor;

        [SerializeField] private bool showSystems;
        
        void OnEnable()
        {
            visitor = new EntityIMGUIVisitor();
        }

        public override void OnInspectorGUI()
        {
            var targetProxy = (EntitySelectionProxy) target;
            if (!targetProxy.Exists)
                return;
            var container = targetProxy.Container;
            targetProxy.Container.PropertyBag.VisitStruct(ref container, visitor);

            GUI.enabled = true;
            showSystems = EditorGUILayout.Foldout(showSystems, "Used by Systems");
            if (showSystems)
            {
                using (var entityComponentTypes = targetProxy.Manager.GetComponentTypes(targetProxy.Entity, Allocator.Temp))
                {
                    var componentGroupList = new List<ComponentGroup>();
                    foreach (var manager in targetProxy.World.BehaviourManagers)
                    {
                        componentGroupList.Clear();
                        var system = manager as ComponentSystemBase;
                        if (system == null) continue;
                        foreach (var componentGroup in system.ComponentGroups)
                        {
                            if (Match(componentGroup, entityComponentTypes))
                                componentGroupList.Add(componentGroup);
                        }
    
                        if (componentGroupList.Count > 0)
                        {
                            GUILayout.Label(manager.GetType().ToString());
                            ++EditorGUI.indentLevel;
                            foreach (var componentGroup in componentGroupList)
                            {
                                if (GUILayout.Button(string.Join(", ", from x in componentGroup.Types.Skip(1) select x.Name)))
                                {
                                    EntityDebugger.FocusSelectionInSystem(system, componentGroup);
                                }
                            }
    
                            --EditorGUI.indentLevel;
                        }
                    }
                }
            }
            
            
        }

        private static bool Match(ComponentGroup group, NativeArray<ComponentType> entityComponentTypes)
        {
            foreach (var subtractiveGroupType in group.SubtractiveTypes)
            {
                foreach (var type in entityComponentTypes)
                {
                    var managedType = type.GetManagedType();
                    if (subtractiveGroupType == managedType)
                    {
                        return false;
                    }
                }
            }
            
            foreach (var groupType in group.Types.Skip(1))
            {
                var found = false;
                foreach (var type in entityComponentTypes)
                {
                    var managedType = type.GetManagedType();
                    if (groupType == managedType)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            return true;
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }
    }
}
