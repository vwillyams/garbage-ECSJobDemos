using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine.ECS;
using UnityEngine.Experimental.LowLevel;

namespace UnityEditor.ECS
{
	public class SystemWindow : EditorWindow
	{
		private PlayerLoopListView playerLoopListView;
		[SerializeField] private TreeViewState playerLoopListState = new TreeViewState();
		
		private SystemGraphView systemGraphView;
		[SerializeField] private List<SystemViewData> systemViews;
		
		Type[] systemTypes
		{
			get
			{
				if (World.Active == null)
					return null;
				return  (from s in World.Active.BehaviourManagers
					where s is ComponentSystemBase
					select s.GetType() ).ToArray();
			}
		}

		void Initialize()
		{
			if (systemGraphView == null)
				systemGraphView = new SystemGraphView();
			
			if (playerLoopListView == null)
				playerLoopListView = new PlayerLoopListView(playerLoopListState);
			OnSetPlayerLoop(PlayerLoopHelper.currentPlayerLoop);
		}
		
		[MenuItem("Window/Systems", false, 2017)]
		static void Open()
		{
			EditorWindow.GetWindow<SystemWindow>("Systems");
		}

		void OnSetPlayerLoop(PlayerLoopSystem playerLoop)
		{
		    systemGraphView.SetSystems(systemTypes, ref systemViews);
			playerLoopListView.UpdatePlayerLoop(playerLoop, new HashSet<string>(from v in systemViews select v.fullName));
		}

		void OnEnable()
		{
			Initialize();
			PlayerLoopHelper.OnUpdatePlayerLoop += OnSetPlayerLoop;
		}

		void OnDisable()
		{
			PlayerLoopHelper.OnUpdatePlayerLoop -= OnSetPlayerLoop;
		}

		void OnGUI()
		{
			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical();
			
			GUILayout.FlexibleSpace();
			
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Layout"))
			{
				systemGraphView.GraphLayout();
			}
			GUILayout.Space(5f);
			if (GUILayout.Button("Clear"))
			{
				systemViews.Clear();
			}
			GUILayout.EndHorizontal();

			if (systemViews.Count == 0)
			{
				GUIHelpers.ShowCenteredNotification(new Rect(Vector2.zero, position.size), "No ComponentSystems loaded. (Try pushing Play)");
				return;
			}
			
			systemGraphView.OnGUIArrows();

			BeginWindows();

			systemGraphView.OnGUIWindows();
			
			EndWindows();
			
			GUILayout.EndVertical();
			
			GUILayout.BeginVertical(GUILayout.Width(300f));
			playerLoopListView.OnGUI(GUIHelpers.GetExpandingRect());
			GUILayout.EndVertical();
			
			GUILayout.EndHorizontal();
		}
	}
	
}
