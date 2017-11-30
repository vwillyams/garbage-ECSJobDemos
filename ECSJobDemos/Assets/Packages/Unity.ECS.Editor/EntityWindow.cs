using System;
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
        const int kDefaultSystemListWidth = 300;
        const float kMinListWidth = 200f;
        const float kSystemListHeight = 100f;

        public ComponentSystemBase CurrentSystemSelection {
            get { return currentSystemSelection; }
            set {
                currentSystemSelection = value;
                currentComponentGroupSelection = null;
                InitComponentGroupList();
                InitEntityList();
            }
        }
        ComponentSystemBase currentSystemSelection;

        void InitComponentGroupList()
        {
            var groupListState = ComponentGroupListView.GetStateForSystem(currentSystemSelection, ref componentGroupListStates, ref componentGroupListStateNames);
            _componentGroupListView = new ComponentGroupListView(groupListState, this, currentSystemSelection);
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

        ComponentGroupListView _componentGroupListView;

        [SerializeField]
        TreeViewState entityListState;

        EntityListView entityListView;

        [SerializeField]
        List<MultiColumnHeaderState> entityColumnHeaderStates;

        [SerializeField]
        List<string> componentGroupListStateNames;

        [SerializeField]
        List<TreeViewState> componentGroupListStates;

        [NonSerialized]
        bool initialized;

        [NonSerialized]
        bool systemsNull = true;

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

        ComponentSystemBase[] systems {
            get {
                if (World.Root == null)
                    return null;

                return  (from s in World.Root.BehaviourManagers
                        where s is ComponentSystemBase
                        select s as ComponentSystemBase).ToArray();
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

        void ComponentGroupList()
        {
            if (CurrentSystemSelection != null)
            {
                _componentGroupListView.OnGUI(GetExpandingRect());
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

        void ShowNoSystemsNotification()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("No ComponentSystems loaded. (Try pushing Play)");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        void OnGUI ()
        {
            InitIfNeeded();
            var systemsWereNull = systemsNull;
            systemsNull = systems == null;
            if (systemsNull)
            {
                ShowNoSystemsNotification();
                return;
            }

            GUILayout.BeginHorizontal(GUILayout.Height(kSystemListHeight));

            GUILayout.BeginVertical();
            SystemList(systemsWereNull);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            ComponentGroupList();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginVertical();
            EntityList();
            GUILayout.EndVertical();
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