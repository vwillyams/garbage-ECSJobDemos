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

        private readonly List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>> cachedMatches = new List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>>();

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
            targetProxy.Container.PropertyBag.Visit(ref container, visitor);

            GUI.enabled = true;
            
            ++EditorGUI.indentLevel;
            GUILayout.BeginVertical(GUI.skin.box);
            showSystems = EditorGUILayout.Foldout(showSystems, "Used by Systems");
            
            if (showSystems)
            {
                if (cachedMatches.Count == 0)
                {
                    CollectMatches(targetProxy);
                }
    
                foreach (var pair in cachedMatches)
                {
                    var type = pair.Item1.GetType();
                    GUILayout.Label(new GUIContent(type.Name, type.AssemblyQualifiedName));
                    ++EditorGUI.indentLevel;
                    foreach (var componentGroup in pair.Item2)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(string.Join(", ", from x in componentGroup.Types.Skip(1) select x.ToString())))
                        {
                            EntityDebugger.FocusSelectionInSystem(pair.Item1 as ComponentSystemBase, componentGroup);
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }

                    --EditorGUI.indentLevel;
                }

                if (Event.current.type == EventType.Repaint)
                {
                    cachedMatches.Clear();
                }
            }
            GUILayout.EndVertical();
        }

        private void CollectMatches(EntitySelectionProxy targetProxy)
        {
            using (var entityComponentTypes = targetProxy.Manager.GetComponentTypes(targetProxy.Entity, Allocator.Temp))
            {
                foreach (var manager in targetProxy.World.BehaviourManagers)
                {
                    var componentGroupList = new List<ComponentGroup>();
                    var system = manager as ComponentSystemBase;
                    if (system == null) continue;
                    foreach (var componentGroup in system.ComponentGroups)
                    {
                        if (Match(componentGroup, entityComponentTypes))
                            componentGroupList.Add(componentGroup);
                    }

                    if (componentGroupList.Count > 0)
                    {
                        cachedMatches.Add(new Tuple<ScriptBehaviourManager, List<ComponentGroup>>(manager, componentGroupList));
                    }
                }
            }
        }

        private static bool Match(ComponentGroup group, NativeArray<ComponentType> entityComponentTypes)
        {
            
            foreach (var groupType in group.Types.Skip(1))
            {
                var found = false;
                foreach (var type in entityComponentTypes)
                {
                    if (type.TypeIndex != groupType.TypeIndex)
                        continue;
                    found = true;
                    break;
                }

                if (found == (groupType.AccessModeType == ComponentType.AccessMode.Subtractive))
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
