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
        TreeViewState m_SystemListState;

        SystemListView m_SystemListView;

        [SerializeField]
        TreeViewState m_TupleListState;

        TupleListView m_TupleListView;

        Rect systemListRect { get { return new Rect(0f, 0f, kSystemListWidth, position.height); } }
        Rect verticalSplitterRect { get { return new Rect(kSystemListWidth, 0f, 1f, position.height); } }
        Rect tupleListRect { get { return new Rect(kSystemListWidth, 0f, position.width - kSystemListWidth, position.height); } }

        [MenuItem ("Window/Entities", false, 2017)]
        static void Init ()
        {
            EditorWindow.GetWindow<EntityWindow>("Entities");
        }

        void OnEnable()
        {
            if (m_SystemListState == null)
                m_SystemListState = new TreeViewState();
            m_SystemListView = new SystemListView(m_SystemListState, this);
            if (m_TupleListState == null)
                m_TupleListState = new TreeViewState();
            m_TupleListView = new TupleListView(m_TupleListState, this);
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

        void SystemList(Rect rect)
        {
            m_SystemListView.SetManagers(systems);
            m_SystemListView.OnGUI(rect);
        }

        void TupleList(Rect rect)
        {
            if (CurrentSelection != null)
            {
                m_TupleListView.SetSelection(CurrentSelection);
                m_TupleListView.OnGUI(rect);
            }
        }

        void OnGUI ()
        {
            if (systems == null)
            {
                GUILayout.Label("No ComponentSystems loaded. (Try pushing Play)");
                return;
            }
            SystemList(systemListRect);
            DrawHorizontalSplitter(verticalSplitterRect);
            TupleList(tupleListRect);
            
        }

        internal static void DrawHorizontalSplitter(Rect dragRect)
        {
            if (Event.current.type != EventType.repaint)
                return;

            Color orgColor = GUI.color;
            Color tintColor = (EditorGUIUtility.isProSkin) ? new Color(0.12f, 0.12f, 0.12f, 1.333f) : new Color(0.6f, 0.6f, 0.6f, 1.333f);
            GUI.color = GUI.color * tintColor;
            Rect splitterRect = new Rect(dragRect.x - 1, dragRect.y, 1, dragRect.height);
            GUI.DrawTexture(splitterRect, EditorGUIUtility.whiteTexture);
            GUI.color = orgColor;
        }
    }
}