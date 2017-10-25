using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace UnityEngine.ECS
{
    public class EntityWindow : EditorWindow {
        
        const float kResizerWidth = 5f;
        [SerializeField]
        float systemListWidth = 300f;
        const float kMinListWidth = 200f;
        const float kSystemListHeight = 200f;

        public GameObject componentDataHolder { get { return entityWrapperHolder; } }
        [SerializeField]
        GameObject entityWrapperHolder;

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
            var headerState = EntityListView.BuildHeaderState(currentComponentGroupSelection);
            var header = new MultiColumnHeader(headerState);
            entityListView = new EntityListView(entityListState, header, currentComponentGroupSelection, this);
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

        Rect systemListRect { get { return new Rect(0f, 0f, systemListWidth, kSystemListHeight); } }
        [SerializeField]
        Rect verticalSplitterRect = new Rect(kMinListWidth, 0f, 1f, kSystemListHeight);
        Rect tupleListRect { get { return new Rect(systemListWidth, 0f, position.width - systemListWidth, kSystemListHeight); } }
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
            if (currentComponentGroupSelection != null)
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

            Rect dragRect = new Rect(systemListWidth, 0f, kResizerWidth, kSystemListHeight);
            dragRect = EditorGUIUtility.HandleHorizontalSplitter(dragRect, position.width, kMinListWidth, kMinListWidth);
            systemListWidth = dragRect.x;

            SystemList(systemListRect);
            EditorGUIUtility.DrawHorizontalSplitter(dragRect);
            TupleList(tupleListRect);
            EntityList(entityListRect);
        }
    }
}