﻿
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

        public void OnGUI(World world, Entity entity)
        {
            ++EditorGUI.indentLevel;
            GUILayout.BeginVertical(GUI.skin.box);
            showSystems = EditorGUILayout.Foldout(showSystems, "Used by Systems");

            if (showSystems)
            {
                if (repainted == true)
                {
                    cachedMatches.Clear();
                    WorldDebuggingTools.MatchEntityInComponentGroups(world, entity, cachedMatches);
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
                            EntityDebugger.SetAllSelections(world, pair.Item1 as ComponentSystemBase, componentGroup, entity);
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
    }
}
