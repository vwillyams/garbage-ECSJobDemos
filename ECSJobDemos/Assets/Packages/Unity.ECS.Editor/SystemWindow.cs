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
		private readonly Rect kStartPosition = new Rect(0f, 0f, 1f, 1f);
		private const float kArrowSize = 11f;
		private const float kLineWidth = 2f;
		private const float kLayerHeight = 50f;
		private const float kHorizontalSpacing = 200f;
		
		[System.Serializable]
		public class SystemViewData
		{
			[SerializeField]
			public string name;
			public string fullName;
			public Rect position;
			public List<int> updateAfter;
			public List<int> updateBefore;

			public SystemViewData(string name, string fullName, Rect position)
			{
				this.name = name;
				this.fullName = fullName;
				this.position = position;
			}

			public Vector3 Center
			{
				get
				{
					var center = (Vector3)position.center;
					center.z = -kArrowSize;
					return center;
				}
			}
		}

		[SerializeField] private List<SystemViewData> systemViews;
		[SerializeField] private TreeViewState playerLoopListState = new TreeViewState();
		private PlayerLoopListView playerLoopListView;
		
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
			if (systemViews == null)
				systemViews = new List<SystemViewData>();
			
			var systemViewIndicesByType = new Dictionary<Type, int>();
			
			if (systemTypes != null)
			{
				foreach (var type in systemTypes)
				{
					var systemViewIndex = systemViews.FindIndex(x => x.fullName == type.FullName);
					if (systemViewIndex < 0)
					{
						systemViews.Add(new SystemViewData(type.Name, type.FullName, kStartPosition));
						systemViewIndex = systemViews.Count - 1;
					}
					systemViews[systemViewIndex].updateAfter = new List<int>();
					systemViews[systemViewIndex].updateBefore = new List<int>();
					systemViewIndicesByType.Add(type, systemViewIndex);
				}
				foreach (var systemType in systemTypes)
				{
					var systemView = systemViews[systemViewIndicesByType[systemType]];
					foreach (var attribute in systemType.GetCustomAttributesData())
					{
						if (attribute.AttributeType == typeof(UpdateAfter))
						{
							var type = (Type)attribute.ConstructorArguments[0].Value;
							if (systemViewIndicesByType.ContainsKey(type))
								systemView.updateAfter.Add(systemViewIndicesByType[type]);
						}
						if (attribute.AttributeType == typeof(UpdateBefore))
						{
							var type = (Type)attribute.ConstructorArguments[0].Value;
							if (systemViewIndicesByType.ContainsKey(type))
								systemView.updateBefore.Add(systemViewIndicesByType[type]);
						}
					}
				}
			}
			if (playerLoopListView == null)
				playerLoopListView = new PlayerLoopListView(playerLoopListState);
			OnSetPlayerLoop(PlayerLoopHelper.currentPlayerLoop);
		}

		void GraphLayout()
		{
			var rowLength = Mathf.RoundToInt(Mathf.Sqrt(systemViews.Count));
			if (rowLength == 0)
				return;
			var x = 0;
			var y = 0;
			foreach (var systemView in systemViews)
			{
				systemView.position.position = new Vector2(x*kHorizontalSpacing, y*kLayerHeight);
				++x;
				x = x % rowLength;
				if (x == 0)
					++y;
			}
		}
		
		[MenuItem("Window/Systems", false, 2017)]
		static void Open()
		{
			EditorWindow.GetWindow<SystemWindow>("Systems");
		}

		void OnSetPlayerLoop(PlayerLoopSystem playerLoop)
		{
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
		
		void ShowNoSystemsNotification()
		{
			GUILayout.BeginArea(new Rect(Vector2.zero, position.size));
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label("No ComponentSystems loaded. (Try pushing Play)");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.EndArea();
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
				GraphLayout();
			}
			GUILayout.Space(5f);
			if (GUILayout.Button("Clear"))
			{
				systemViews.Clear();
			}
			GUILayout.EndHorizontal();

			if (systemViews.Count == 0)
			{
				ShowNoSystemsNotification();
				return;
			}
			
			foreach (var systemView in systemViews)
			{
				foreach (var typeIndex in systemView.updateAfter)
				{
					DrawArrowBetweenBoxes(systemView, systemViews[typeIndex]);
				}
				foreach (var typeIndex in systemView.updateBefore)
				{
					DrawArrowBetweenBoxes(systemViews[typeIndex], systemView);
				}
			}

			BeginWindows();
			
			for (var i = 0; i < systemViews.Count; ++i)
			{
				var view = systemViews[i];
				view.position = GUILayout.Window(i, view.position, WindowFunction, "", GUI.skin.box);
			}
			
			EndWindows();
			
			GUILayout.EndVertical();
			
			GUILayout.BeginVertical(GUILayout.Width(300f));
			playerLoopListView.OnGUI(GetExpandingRect());
			GUILayout.EndVertical();
			
			GUILayout.EndHorizontal();
		}

		Rect GetExpandingRect()
		{
			return GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
		}

		private void DrawArrowBetweenBoxes(SystemViewData fromView, SystemViewData toView)
		{
			var arrowDirection = toView.Center - fromView.Center;
			if (arrowDirection == Vector3.zero)
				return;
			Handles.color = EditorStyles.label.normal.textColor;
			var lineTexture = (Texture2D) EditorGUIUtility.LoadRequired("AALineRetina.png");
			var startPos = ExteriorPointFromOtherPoint(fromView.position, toView.Center);
			var endPos = ExteriorPointFromOtherPoint(toView.position, fromView.Center);
			endPos -= (endPos - startPos).normalized * 0.6f * kArrowSize;
			Handles.DrawAAPolyLine(lineTexture, EditorGUIUtility.pixelsPerPoint * kLineWidth, startPos, endPos);
			var rotation = Quaternion.LookRotation(arrowDirection, Vector3.forward);
			Handles.ConeHandleCap(0, endPos, rotation, kArrowSize, Event.current.type);
		}

		static Vector3 ExteriorPointFromOtherPoint(Rect rect, Vector2 other)
		{
			if (rect.width == 0f || rect.height == 0f)
				return rect.center;
			var localOther = other - rect.center;
			var ext = localOther;
			ext.x = Mathf.Abs(ext.x) / (rect.width*0.5f);
			ext.y = Mathf.Abs(ext.y) / (rect.height*0.5f);

			if (ext.x > ext.y)
			{
				ext.y /= ext.x;
				ext.x = 1f;
			}
			else if (ext.y > ext.x)
			{
				ext.x /= ext.y;
				ext.y = 1f;
			}
			ext.x *= Mathf.Sign(localOther.x)*(rect.width*0.5f);
			ext.y *= Mathf.Sign(localOther.y)*(rect.height*0.5f);
			ext += rect.center;
			
			return new Vector3(ext.x, ext.y, -5f);
		}

		void WindowFunction(int id)
		{
			GUI.DragWindow(new Rect(0f, 0f, 1000f, 1000f));
			GUILayout.Label(new GUIContent(systemViews[id].name, systemViews[id].fullName));
		}
	}
	
}
