
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

[assembly:InternalsVisibleTo("Unity.Entities.Editor.Tests")]

namespace Unity.Entities.Editor
{
    [System.Serializable]
    public class SystemInclusionList
    {
        private readonly List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>> cachedMatches = new List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>>();
        private bool repainted = true;

        [SerializeField] private bool showSystems;

        public void OnGUI(EntityManager entityManager, Entity entity)
        {
            ++EditorGUI.indentLevel;
            GUILayout.BeginVertical(GUI.skin.box);
            showSystems = EditorGUILayout.Foldout(showSystems, "Used by Systems");

            if (showSystems)
            {
                if (repainted == true)
                {
                    cachedMatches.Clear();
                    CollectMatches(entityManager, entity, cachedMatches);
                    repainted = false;
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
                            EntityDebugger.SetAllSelections(World.Active, pair.Item1 as ComponentSystemBase, componentGroup, entity);
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }

                    --EditorGUI.indentLevel;
                }

                if (Event.current.type == EventType.Repaint)
                {
                    repainted = true;
                }
            }
            GUILayout.EndVertical();

            --EditorGUI.indentLevel;
        }

        internal static void CollectMatches(EntityManager entityManager, Entity entity, List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>> matchList)
        {
            using (var entityComponentTypes = entityManager.GetComponentTypes(entity, Allocator.Temp))
            {
                foreach (var manager in World.Active.BehaviourManagers)
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
                        matchList.Add(new Tuple<ScriptBehaviourManager, List<ComponentGroup>>(manager, componentGroupList));
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
    }
}
