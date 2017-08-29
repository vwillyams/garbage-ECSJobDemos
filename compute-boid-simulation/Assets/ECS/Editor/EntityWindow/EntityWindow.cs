using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace UnityEngine.ECS
{
    public class EntityWindow : EditorWindow {
        
        const float kSystemListWidth = 300f;

        public ComponentSystem CurrentSelection { get; set; }

        [SerializeField]
        TreeViewState m_TreeViewState;

        SystemListView m_EntityListView;

        [MenuItem ("Window/Entities", false, 2017)]
        static void Init ()
        {
            EditorWindow.GetWindow<EntityWindow>("Entities");
        }

        void OnEnable()
        {
            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();
            m_EntityListView = new SystemListView(m_TreeViewState, this);
        }

        ComponentSystem[] systems {
            get {
                if (DependencyManager.Root == null)
                    return null;
                return  (from s in DependencyManager.Root.BehaviourManagers
                        where s is ComponentSystem
                        select s as ComponentSystem).ToArray();
            }
        }

        void SystemList()
        {
            m_EntityListView.SetManagers(systems);
            m_EntityListView.OnGUI(new Rect(0f, 0f, kSystemListWidth, position.height));
        }

        void TupleList()
        {
            GUILayout.BeginArea(new Rect(kSystemListWidth, 0f, position.width - kSystemListWidth, position.height), GUIContent.none, "OL box");
            if (CurrentSelection != null)
            {
                var tupleIndex = 0;
                foreach (var tupleSystem in CurrentSelection.Tuples)
                {
                    var types = new List<Type>();
                    types.AddRange(tupleSystem.ComponentTypes);
                    types.AddRange(tupleSystem.ComponentDataTypes);
                    if (tupleSystem.HasTransformAccess)
                        types.Add(typeof(TransformAccess));
                    var tupleName = string.Join(", ", (from x in types select x.Name).ToArray());
                    var components = tupleSystem.GetEntityArray();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Format("({1}):", tupleIndex, tupleName));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(components.Length.ToString());
                    GUILayout.EndHorizontal();
                    ++tupleIndex;
                }
            }
            GUILayout.EndArea();
        }

        void OnGUI ()
        {
            if (systems == null)
            {
                GUILayout.Label("No ComponenySystems loaded. (Try pushing Play)");
                return;
            }
            SystemList();
            TupleList();
        }
    }
}