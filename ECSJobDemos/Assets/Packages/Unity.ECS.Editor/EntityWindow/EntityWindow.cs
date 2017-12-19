using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.ECS;
using Unity.Jobs;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.ECS
{
    public class EntityWindow : EditorWindow {
        
        const float kSystemListHeight = 100f;

        public World CurrentWorldSelection
        {
            get { return currentWorldSelection; }
            set
            {
                currentWorldSelection = value;
                currentSystemSelection = null;
                currentComponentGroupSelection = null;
                InitSystemList();
                InitComponentGroupList();
                InitEntityList();
            }
        }
        World currentWorldSelection;

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

        public ComponentGroup CurrentComponentGroupSelection {
            get { return currentComponentGroupSelection; }
            set {
                currentComponentGroupSelection = value;
                InitEntityList();
            }
        }
        ComponentGroup currentComponentGroupSelection;

        void InitSystemList()
        {
            if (currentWorldSelection == null)
                return;
            systemListView = new SystemListView(systemListState, this);
            systemListView.SetManagers(systems);
        }
        
        void InitComponentGroupList()
        {
            if (currentSystemSelection == null)
                return;
            var groupListState = ComponentGroupListView.GetStateForSystem(currentSystemSelection, ref componentGroupListStates, ref componentGroupListStateNames);
            componentGroupListView = new ComponentGroupListView(groupListState, this, currentSystemSelection);
        }

        void InitEntityList()
        {
            if (currentComponentGroupSelection == null)
                return;
            entityListState = new TreeViewState();
            var headerState = EntityListView.GetOrBuildHeaderState(ref entityColumnHeaderStates, currentComponentGroupSelection, position.width - GUI.skin.verticalScrollbar.fixedWidth);
            var header = new MultiColumnHeader(headerState);
            entityListView = new EntityListView(entityListState, header, currentComponentGroupSelection);
        }

        [SerializeField] TreeViewState worldListState;
        WorldListView worldListView;

        [SerializeField]
        TreeViewState systemListState;

        SystemListView systemListView;

        ComponentGroupListView componentGroupListView;

        [SerializeField]
        TreeViewState entityListState;

        EntityListView entityListView;

        [SerializeField]
        List<MultiColumnHeaderState> entityColumnHeaderStates;

        [SerializeField]
        List<string> componentGroupListStateNames;

        [SerializeField]
        List<TreeViewState> componentGroupListStates;

        [MenuItem ("Window/Entities", false, 2017)]
        static void Init ()
        {
            EditorWindow.GetWindow<EntityWindow>("Entities");
        }

        void OnEnable()
        {
            if (worldListState == null)
                worldListState = new TreeViewState();
            worldListView = new WorldListView(worldListState, this);
        }

        void OnFocus()
        {
            SceneView.onSceneGUIDelegate += OnSceneGUI;
        }

        void OnLostFocs()
        {
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
        }

        private ReadOnlyCollection<World> Worlds => worlds ?? (worlds = World.AllWorlds);
        private ReadOnlyCollection<World> worlds;
        
        ComponentSystemBase[] systems => (from s in CurrentWorldSelection.BehaviourManagers
            where s is ComponentSystemBase
            select s as ComponentSystemBase).ToArray();

        void WorldList(bool worldsAppeared)
        {
            if (worldsAppeared)
                worldListView.SetWorlds(Worlds);
            worldListView.OnGUI(GUIHelpers.GetExpandingRect());
        }

        void SystemList()
        {
            if (CurrentWorldSelection != null)
            {
                systemListView.OnGUI(GUIHelpers.GetExpandingRect());
            }
        }

        void ComponentGroupList()
        {
            if (CurrentSystemSelection != null)
            {
                componentGroupListView.OnGUI(GUIHelpers.GetExpandingRect());
            }
        }

        void EntityList()
        {
            if (currentComponentGroupSelection != null)
            {
                entityListView.PrepareData();
                entityListView.OnGUI(GUIHelpers.GetExpandingRect());
            }
        }

        private bool noWorlds = true;

        void OnGUI ()
        {
            var worldsAppeared = noWorlds && World.AllWorlds.Count > 0;
            noWorlds = World.AllWorlds.Count == 0;
            if (noWorlds)
            {
                GUIHelpers.ShowCenteredNotification(new Rect(Vector2.zero, position.size), "No ComponentSystems loaded. (Try pushing Play)");
                return;
            }

            GUILayout.BeginHorizontal(GUILayout.Height(kSystemListHeight));

            GUILayout.BeginVertical();
            WorldList(worldsAppeared);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            SystemList();
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
            entityListView?.DrawSelection();

            if (CurrentSystemSelection != null && EditorApplication.isPlaying)
                Repaint();
        }
    }
}