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
            using (var types = targetProxy.Manager.GetComponentTypes(targetProxy.Entity, Allocator.Temp))
            {
                var componentGroupList = new List<ComponentGroup>();
                foreach (var manager in targetProxy.World.BehaviourManagers)
                {
                    componentGroupList.Clear();
                    var system = manager as ComponentSystemBase;
                    if (system == null) continue;
                    foreach (var componentGroup in system.ComponentGroups)
                    {
                        if (Match(componentGroup, types))
                            componentGroupList.Add(componentGroup);
                    }

                    if (componentGroupList.Count > 0)
                    {
                        GUILayout.Label(manager.GetType().ToString());
                        ++EditorGUI.indentLevel;
                        foreach (var componentGroup in componentGroupList)
                        {
                            if (GUILayout.Button(string.Join<Type>(", ", componentGroup.Types)))
                            {
                                EntityDebugger.FocusSelectionInSystem(system, componentGroup);
                            }
                        }

                        --EditorGUI.indentLevel;
                    }
                }
            }
            
            
        }

        private static bool Match(ComponentGroup group, NativeArray<ComponentType> types)
        {
            foreach (var groupType in group.Types.Skip(1))
            {
                var found = false;
                foreach (var type in types)
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
