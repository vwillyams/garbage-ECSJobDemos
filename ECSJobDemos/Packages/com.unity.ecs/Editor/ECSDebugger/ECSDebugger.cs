using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.ECS;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.ECS
{
    public class ECSDebugger : EditorWindow, ISystemSelectionWindow, IEntitySelectionWindow {
        private const float kSystemListWidth = 350f;

        [MenuItem("Window/ECS Debugger", false, 2017)]
        static void OpenWindow()
        {
            GetWindow<ECSDebugger>("ECS Debugger");
        }

        private static GUIStyle Box
        {
            get
            {
                if (box == null)
                {
                    box = new GUIStyle(GUI.skin.box);
                    box.margin = new RectOffset();
                    box.padding = new RectOffset(1, 1, 1, 1);
                }

                return box;
            }
        }

        private static GUIStyle box;

        public ScriptBehaviourManager SystemSelection
        {
            get { return systemSelection; }
            set
            {
                systemSelection = value;
                componentListView.SelectedSystem = systemSelection as ComponentSystemBase;
            }
        }

        private ScriptBehaviourManager systemSelection;

        public Entity EntitySelection { get; set; }

        [SerializeField]
        private TreeViewState systemListState = new TreeViewState();
        
        private GroupedSystemListView systemListView;
        
        [SerializeField]
        private TreeViewState componentListState = new TreeViewState();

        private ComponentGroupIntegratedListView componentListView;

        private string[] worldNames => (from x in World.AllWorlds select x.Name).ToArray();

        private void SelectWorldByName(string name)
        {
            foreach (var world in World.AllWorlds)
            {
                if (world.Name == name)
                {
                    WorldSelection = world;
                    return;
                }
            }

            WorldSelection = null;
        }
        
        public World WorldSelection
        {
            get { return worldSelection; }
            set
            {
                if (worldSelection != value)
                {
                    worldSelection = value;
                    if (worldSelection != null)
                        lastSelectedWorldName = worldSelection.Name;
                    systemListView.SetWorld(worldSelection);
                }
            }
        }

        private World worldSelection;
        [SerializeField] private string lastSelectedWorldName;

        private int selectedWorldIndex
        {
            get { return World.AllWorlds.IndexOf(WorldSelection); }
            set
            {
                if (value >= 0 && value < World.AllWorlds.Count)
                    WorldSelection = World.AllWorlds[value];
            }
        }

        private bool worldsExist;

        private readonly string[] noWorldsName = new[] {"No worlds"};
        private bool worldsAppeared;

        void OnEnable()
        {
            var systemListHeader = new MultiColumnHeader(GroupedSystemListView.GetHeaderState());
            systemListView = new GroupedSystemListView(systemListState, systemListHeader, this);
            componentListView =
                new ComponentGroupIntegratedListView(componentListState, this, SystemSelection as ComponentSystemBase);
        }

        void WorldPopup()
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
                if (worldsAppeared && WorldSelection == null)
                {
                    SelectWorldByName(lastSelectedWorldName);
                    if (WorldSelection == null)
                    {
                        WorldSelection = World.AllWorlds[0];
                    }
                }
                selectedWorldIndex = EditorGUILayout.Popup(selectedWorldIndex, worldNames);
            }
        }

        void SystemList()
        {
            var rect = GUIHelpers.GetExpandingRect();
            if (worldsExist)
            {
                if (worldsAppeared)
                    systemListView.multiColumnHeader.ResizeToFit();
                systemListView.OnGUI(rect);
            }
            else
            {
                GUIHelpers.ShowCenteredNotification(rect, "No systems (Try pushing Play)");
            }
        }

        void SystemHeader()
        {

            GUILayout.BeginHorizontal();
            GUILayout.Label("Systems", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            AlignHeader(WorldPopup);
            GUILayout.EndHorizontal();
        }

        void EntityHeader()
        {
            GUILayout.BeginHorizontal();
            var type = systemSelection.GetType();
            AlignHeader(() => GUILayout.Label(type.Namespace, EditorStyles.label));
            GUILayout.Label(type.Name, EditorStyles.boldLabel);
            if (SystemSelection is ComponentSystemBase)
            {
                GUILayout.FlexibleSpace();

                var system = (ComponentSystemBase) SystemSelection;
                var running = system.Enabled && system.ShouldRunSystem();
                AlignHeader(() => GUILayout.Label($"running: {running}"));
            }
            GUILayout.EndHorizontal();
        }

        void ComponentList()
        {
            componentListView.OnGUI(GUIHelpers.GetExpandingRect());
        }

        void AlignHeader(System.Action action)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(6f);
            action();
            GUILayout.EndVertical();
        }

        void OnGUI()
        {
            var worldsExisted = worldsExist;
            worldsExist = World.AllWorlds.Count > 0;
            worldsAppeared = !worldsExisted && worldsExist;
            
            GUILayout.BeginHorizontal();
            
            GUILayout.BeginVertical(GUILayout.Width(kSystemListWidth)); // begin System List
            SystemHeader();
            
            GUILayout.BeginVertical();
            SystemList();
            GUILayout.EndVertical();
            
            GUILayout.EndVertical(); // end System List
            
            GUILayout.BeginVertical(GUILayout.Width(position.width - kSystemListWidth)); // begin Entity List

            if (SystemSelection != null)
            {
                EntityHeader();
            
                GUILayout.BeginVertical(Box);
                ComponentList();
                GUILayout.EndVertical();
            }
            
            GUILayout.EndVertical(); // end Component List
            
            GUILayout.EndHorizontal();
        }
    }
}