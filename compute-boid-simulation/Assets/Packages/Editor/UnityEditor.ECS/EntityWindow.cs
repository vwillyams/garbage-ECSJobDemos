﻿using System;
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

        public GameObject componentDataHolder { get { return entityWrapperHolder; } }
        [SerializeField]
        GameObject entityWrapperHolder;

        public ComponentSystem CurrentSystemSelection {
            get { return currentSystemSelection; }
            set {
                currentSystemSelection = value;
                currentTupleSelection = null;
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

        public TupleSystem CurrentTupleSelection {
            get { return currentTupleSelection; }
            set {
                currentTupleSelection = value;
                InitEntityList();
            }
        }
        TupleSystem currentTupleSelection;

        void InitEntityList()
        {
            if (currentTupleSelection == null)
                return;
            entityListState = new TreeViewState();
            var headerState = EntityListView.BuildHeaderState(currentTupleSelection);
            var header = new MultiColumnHeader(headerState);
            entityListView = new EntityListView(entityListState, header, currentTupleSelection, this);
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

        MultiColumnHeaderState entityListHeaderState;

        [NonSerialized]
        bool initialized;

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

        void OnEnable()
        {
            entityWrapperHolder = EditorUtility.CreateGameObjectWithHideFlags("__EntityWindowWrapperHolder", HideFlags.HideAndDontSave);
        }

        void OnDisable()
        {
            if (entityWrapperHolder != null)
                DestroyImmediate(entityWrapperHolder);
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

        void SystemList(Rect rect)
        {
            systemListView.SetManagers(systems);
            systemListView.OnGUI(rect);
        }

        void TupleList(Rect rect)
        {
            if (CurrentSystemSelection != null)
            {
                tupleListView.OnGUI(rect);
            }
        }

        void EntityList(Rect rect)
        {
            if (CurrentTupleSelection != null)
            {
                entityListView.PrepareData();
                entityListView.OnGUI(rect);
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