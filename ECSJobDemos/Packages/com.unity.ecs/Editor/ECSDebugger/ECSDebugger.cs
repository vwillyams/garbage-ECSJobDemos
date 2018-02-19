using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.ECS;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.ECS
{
    public class ECSDebugger : EditorWindow {

        [MenuItem("Window/ECS Debugger", false, 2017)]
        static void OpenWindow()
        {
            GetWindow<ECSDebugger>("ECS Debugger");
        }

        [SerializeField]
        private TreeViewState systemListState;
        
        private SystemListView systemListView;

        void OnEnable()
        {
//            systemListView = new SystemListView(systemListState, );
        }

        void OnGUI()
        {
            GUILayout.Label("Systems", EditorStyles.boldLabel);
            
            
            
            GUILayout.BeginVertical();
            GUI.Label(GUIHelpers.GetExpandingRect(), "", GUI.skin.box);
            
            GUILayout.EndVertical();
        }
    }
}