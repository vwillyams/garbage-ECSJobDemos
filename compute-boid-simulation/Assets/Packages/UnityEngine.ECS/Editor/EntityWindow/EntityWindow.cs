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
        const float kSystemListHeight = 200f;

        public ComponentSystem CurrentSystemSelection { get; set; }

        public TupleSystem CurrentTupleSelection { get; set; }

        [SerializeField]
        TreeViewState m_SystemListState;

        SystemListView m_SystemListView;

        [SerializeField]
        TreeViewState m_TupleListState;

        TupleListView m_TupleListView;

        [SerializeField]
        TreeViewState m_EntityListState;

        EntityListView m_EntityListView;

        MultiColumnHeader m_EntityListHeader;

        [NonSerialized]
        bool m_Initialized;

        Rect systemListRect { get { return new Rect(0f, 0f, kSystemListWidth, kSystemListHeight); } }
        Rect verticalSplitterRect { get { return new Rect(kSystemListWidth, 0f, 1f, kSystemListHeight); } }
        Rect tupleListRect { get { return new Rect(kSystemListWidth, 0f, position.width - kSystemListWidth, kSystemListHeight); } }
        // Rect horizontalSplitterRect { get { return new Rect(0f, kSystemListHeight, position.width, 1f); } }
        Rect entityListRect { get { return new Rect(0f, kSystemListHeight, position.width, position.height - kSystemListHeight); } }

        [MenuItem ("Window/Entities", false, 2017)]
        static void Init ()
        {
            EditorWindow.GetWindow<EntityWindow>("Entities");
        }

        void InitIfNeeded()
        {
            if (!m_Initialized)
            {
                if (m_SystemListState == null)
                    m_SystemListState = new TreeViewState();
                m_SystemListView = new SystemListView(m_SystemListState, this);
                if (m_TupleListState == null)
                    m_TupleListState = new TreeViewState();
                m_TupleListView = new TupleListView(m_TupleListState, this);
                if (m_EntityListState == null)
                    m_EntityListState = new TreeViewState();
                m_EntityListView = new EntityListView(m_EntityListState, this);
                m_Initialized = true;
            }
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
            if (CurrentSystemSelection != null)
            {
                m_TupleListView.SetSelection(CurrentSystemSelection);
                m_TupleListView.OnGUI(rect);
            }
        }

        void EntityList(Rect rect)
        {
            if (CurrentTupleSelection != null)
            {
                m_EntityListView.SetSelection(CurrentTupleSelection);
                m_EntityListView.OnGUI(rect);
            }
        }

        void OnGUI ()
        {
            InitIfNeeded();
            if (systems == null)
            {
                GUILayout.Label("No ComponentSystems loaded. (Try pushing Play)");
                return;
            }
            SystemList(systemListRect);
            DrawHorizontalSplitter(verticalSplitterRect);
            TupleList(tupleListRect);
            EntityList(entityListRect);
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