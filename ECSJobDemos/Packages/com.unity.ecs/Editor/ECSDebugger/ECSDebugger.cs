using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.ECS;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.ECS
{
    public class ECSDebugger : EditorWindow, ISystemSelectionWindow {

        [MenuItem("Window/ECS Debugger", false, 2017)]
        static void OpenWindow()
        {
            GetWindow<ECSDebugger>("ECS Debugger");
        }

        public ScriptBehaviourManager SystemSelection { get; set; }

        [SerializeField]
        private TreeViewState systemListState = new TreeViewState();
        
        private GroupedSystemListView systemListView;

        private string[] worldNames => (from x in World.AllWorlds select x.Name).ToArray();

        private void SelectWorldByName(string name)
        {
            foreach (var world in World.AllWorlds)
            {
                if (world.Name == name)
                {
                    selectedWorld = world;
                    return;
                }
            }

            selectedWorld = null;
        }
        
        private World selectedWorld
        {
            get { return m_SelectedWorld; }
            set
            {
                if (m_SelectedWorld != value)
                {
                    m_SelectedWorld = value;
                    if (m_SelectedWorld != null)
                        lastSelectedWorldName = m_SelectedWorld.Name;
                    systemListView.SetWorld(m_SelectedWorld);
                }
            }
        }

        private World m_SelectedWorld;
        [SerializeField] private string lastSelectedWorldName;

        private int selectedWorldIndex
        {
            get { return World.AllWorlds.IndexOf(selectedWorld); }
            set
            {
                if (value >= 0 && value < World.AllWorlds.Count)
                    selectedWorld = World.AllWorlds[value];
            }
        }

        private bool worldsExist;

        private readonly string[] noWorldsName = new[] {"No worlds"};

        void OnEnable()
        {
            systemListView = new GroupedSystemListView(systemListState, this);
        }

        void WorldPopup(bool worldsAppeared)
        {
            if (!worldsExist)
            {
                var guiEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.Popup(0, noWorldsName);
                GUI.enabled = guiEnabled;
            }
            else
            {
                if (worldsAppeared && selectedWorld == null)
                    SelectWorldByName(lastSelectedWorldName);
                selectedWorldIndex = EditorGUILayout.Popup(selectedWorldIndex, worldNames);
            }
        }

        void SystemList()
        {
            var rect = GUIHelpers.GetExpandingRect();
            if (worldsExist)
            {
                systemListView.OnGUI(rect);
            }
            else
            {
                GUIHelpers.ShowCenteredNotification(rect, "No systems (Try pushing Play)");
            }
        }

        void OnGUI()
        {
            var worldsExisted = worldsExist;
            worldsExist = World.AllWorlds.Count > 0;
            var worldsAppeared = !worldsExisted && worldsExist;
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Systems", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            WorldPopup(worldsAppeared);
            GUILayout.EndHorizontal();
            
            GUILayout.BeginVertical(GUI.skin.box);
            SystemList();
            
            GUILayout.EndVertical();
        }
    }
}