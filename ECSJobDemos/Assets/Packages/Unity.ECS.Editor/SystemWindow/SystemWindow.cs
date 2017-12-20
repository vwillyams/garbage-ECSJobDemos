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
	public class SystemWindow : EditorWindow, IWorldSelectionWindow
	{
	    const float kWorldListHeight = 100f;

	    private World currentWorldSelection;

	    public void SetWorldSelection(World world)
	    {
	        currentWorldSelection = world;
	        UpdateSystemGraph();
	    }

	    void UpdateSystemGraph()
	    {
	        if (currentWorldSelection == null)
	            return;
	        if (systemGraphView == null)
	            systemGraphView = new SystemGraphView(Vector2.up*kWorldListHeight);
	        var systemGraphState =
	            SystemGraphView.GetStateForWorld(currentWorldSelection, ref systemViews, ref worldNames);
	        systemGraphView.SetSystemsAndState(systemTypes, systemGraphState);
	    }

		private PlayerLoopListView playerLoopListView;
		[SerializeField] private TreeViewState playerLoopListState = new TreeViewState();

	    private WorldListView worldListView;
	    [SerializeField] private TreeViewState worldListState = new TreeViewState();

		private SystemGraphView systemGraphView;
		[SerializeField] private List<SystemGraphState> systemViews;
	    [SerializeField] private List<string> worldNames;

		Type[] systemTypes => (from s in currentWorldSelection.BehaviourManagers
		    where s is ComponentSystemBase
		    select s.GetType() ).ToArray();

	    void Initialize()
		{
		    if (worldListView == null)
		        worldListView = new WorldListView(worldListState, this);

		    UpdateSystemGraph();

			if (playerLoopListView == null)
				playerLoopListView = new PlayerLoopListView(playerLoopListState);
			SetPlayerLoop(PlayerLoopHelper.currentPlayerLoop);
		}

		[MenuItem("Window/Systems", false, 2017)]
		static void Open()
		{
			EditorWindow.GetWindow<SystemWindow>("Systems");
		}

		void SetPlayerLoop(PlayerLoopSystem playerLoop)
		{
		    UpdateSystemGraph();
		    var systemNames = currentWorldSelection == null
		        ? new HashSet<string>()
		        : new HashSet<string>(from t in systemTypes select t.FullName);
			playerLoopListView.UpdatePlayerLoop(playerLoop, systemNames);
		}

		void OnEnable()
		{
			Initialize();
			PlayerLoopHelper.OnUpdatePlayerLoop += SetPlayerLoop;
		}

		void OnDisable()
		{
			PlayerLoopHelper.OnUpdatePlayerLoop -= SetPlayerLoop;
		}

	    ComponentSystemBase[] systems => (from s in currentWorldSelection.BehaviourManagers
	        where s is ComponentSystemBase
	        select s as ComponentSystemBase).ToArray();

	    private bool noWorlds = true;

		void OnGUI()
		{

		    var worldsAppeared = noWorlds && World.AllWorlds.Count > 0;
		    noWorlds = World.AllWorlds.Count == 0;
		    if (noWorlds)
		    {

		    }
            if (worldsAppeared)
                worldListView.SetWorlds(World.AllWorlds);

			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical();
            GUILayout.BeginVertical(GUILayout.Height(kWorldListHeight));
		    worldListView.OnGUI(GUIHelpers.GetExpandingRect());
		    GUILayout.EndVertical();
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

			if (currentWorldSelection == null)
			{
				GUIHelpers.ShowCenteredNotification(new Rect(Vector2.zero, position.size), "No ComponentSystems loaded. (Try pushing Play)");
			}
			else
			{
			    systemGraphView.OnGUIArrows();

			    BeginWindows();

			    systemGraphView.OnGUIWindows();

			    EndWindows();
			}

			GUILayout.EndVertical();

			GUILayout.BeginVertical(GUILayout.Width(300f));
			playerLoopListView.OnGUI(GUIHelpers.GetExpandingRect());
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
		}
	}

}
