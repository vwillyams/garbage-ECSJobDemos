﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Jobs;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.ECS
{
    public class EntityWindow : EditorWindow {
        
        const float kResizerWidth = 5f;
        [SerializeField]
        const int kDefaultSystemListWidth = 300;
        const float kMinListWidth = 200f;
        const float kSystemListHeight = 100f;

        // [SerializeField]
        // SplitterState systemTupleSplitter = new SplitterState(new float[] { 1, 1 }, new int[] { 100, 100 }, null);
        // [SerializeField]
        // SplitterState entityListSplitter = new SplitterState(new float[] { 1, 1 }, new int[] { 100, 100 }, null);

        public ComponentSystem CurrentSystemSelection {
            get { return currentSystemSelection; }
            set {
                currentSystemSelection = value;
                currentComponentGroupSelection = null;
                InitTupleList();
                InitEntityList();
            }
        }
        ComponentSystem currentSystemSelection;

        void InitTupleList()
        {
            tupleListState = new TreeViewState();
            tupleListView = new TupleListView(tupleListState, this);
            tupleListView.SetSelection(currentSystemSelection);
        }

        public ComponentGroup CurrentComponentGroupSelection {
            get { return currentComponentGroupSelection; }
            set {
                currentComponentGroupSelection = value;
                InitEntityList();
            }
        }
        ComponentGroup currentComponentGroupSelection;

        void InitEntityList()
        {
            if (currentComponentGroupSelection == null)
                return;
            entityListState = new TreeViewState();
            var headerState = EntityListView.GetOrBuildHeaderState(ref entityColumnHeaderStates, currentComponentGroupSelection, position.width - GUI.skin.verticalScrollbar.fixedWidth);
            var header = new MultiColumnHeader(headerState);
            entityListView = new EntityListView(entityListState, header, currentComponentGroupSelection);
        }

        [SerializeField]
        TreeViewState systemListState;

        SystemListView systemListView;

        [SerializeField]
        TreeViewState tupleListState;

        TupleListView tupleListView;

        [SerializeField]
        TreeViewState entityListState;

        EntityListView entityListView;

        [SerializeField]
        List<MultiColumnHeaderState> entityColumnHeaderStates;

        [NonSerialized]
        bool initialized;

        [NonSerialized]
        bool systemsNull = true;

        // Rect systemListRect { get { return new Rect(0f, 0f, systemListWidth, kSystemListHeight); } }
        // [SerializeField]
        // Rect verticalSplitterRect = new Rect(kMinListWidth, 0f, 1f, kSystemListHeight);
        // Rect tupleListRect { get { return new Rect(systemListWidth, 0f, position.width - systemListWidth, kSystemListHeight); } }
        // Rect horizontalSplitterRect { get { return new Rect(0f, kSystemListHeight, position.width, 1f); } }
        Rect entityListRect { get { return new Rect(0f, kSystemListHeight, position.width, position.height - kSystemListHeight); } }

        [MenuItem ("Window/Entities", false, 2017)]
        static void Init ()
        {
            EditorWindow.GetWindow<EntityWindow>("Entities");
        }

        void OnFocus()
        {
            SceneView.onSceneGUIDelegate += OnSceneGUI;
        }

        void OnLostFocs()
        {
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
        }

        void InitIfNeeded()
        {
            if (!initialized)
            {
                if (systemListState == null)
                    systemListState = new TreeViewState();
                systemListView = new SystemListView(systemListState, this);
                initialized = true;
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

        Rect GetExpandingRect()
        {
            return GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        }

        void SystemList(bool systemsWereNull)
        {
            if (systemsWereNull)
                systemListView.SetManagers(systems);
            systemListView.OnGUI(GetExpandingRect());
        }

        void TupleList()
        {
            if (CurrentSystemSelection != null)
            {
                tupleListView.OnGUI(GetExpandingRect());
            }
        }

        void EntityList()
        {
            if (currentComponentGroupSelection != null)
            {
                entityListView.PrepareData();
                entityListView.OnGUI(GetExpandingRect());
            }
        }

        void OnGUI ()
        {
            InitIfNeeded();
            var systemsWereNull = systemsNull;
            systemsNull = systems == null;
            if (systemsNull)
            {
                GUILayout.Label("No ComponentSystems loaded. (Try pushing Play)");
                return;
            }

            // SplitterGUILayout.BeginVerticalSplit(entityListSplitter);

            // SplitterGUILayout.BeginHorizontalSplit(systemTupleSplitter);
            GUILayout.BeginHorizontal(GUILayout.Height(kSystemListHeight));

            GUILayout.BeginVertical();
            SystemList(systemsWereNull);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            TupleList();
            GUILayout.EndVertical();

            // SplitterGUILayout.EndHorizontalSplit();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            EntityList();
            GUILayout.EndVertical();

            // SplitterGUILayout.EndVerticalSplit();

        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (entityListView != null)
                entityListView.DrawSelection();

            if (CurrentSystemSelection != null && EditorApplication.isPlaying)
                Repaint();
        }
    }
}