using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.ECS;

namespace UnityEditor.ECS
{

	public class SystemWindow : EditorWindow
	{
		private readonly Rect kStartPosition = new Rect(0f, 0f, 1f, 1f);
		private const float kArrowSize = 10f;
		private const float kLineWidth = 2f;
		
		[System.Serializable]
		public class SystemViewData
		{
			public string name;
			public string fullName;
			public Rect position;
			public List<int> updateAfter;

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
					center.z = -5f;
					return center;
				}
			}
		}

		[SerializeField] private List<SystemViewData> systemViews;

		void Initialize()
		{
			if (systemViews == null)
				systemViews = new List<SystemViewData>();
			
			var systemViewIndicesByType = new Dictionary<Type, int>();

			if (ScriptBehaviourUpdateOrder.dependencyGraph != null)
			{
				var systemTypes = (from t in ScriptBehaviourUpdateOrder.dependencyGraph.Keys select t).ToArray();
				foreach (var type in systemTypes)
				{
					var systemViewIndex = systemViews.FindIndex(x => x.fullName == type.FullName);
					if (systemViewIndex < 0)
					{
						systemViews.Add(new SystemViewData(type.Name, type.FullName, kStartPosition));
						systemViewIndex = systemViews.Count - 1;
					}
					systemViews[systemViewIndex].updateAfter = new List<int>();
					systemViewIndicesByType.Add(type, systemViewIndex);
				}
				foreach (var systemType in systemTypes)
				{
					var systemViewIndex = systemViewIndicesByType[systemType];
					var systemView = systemViews[systemViewIndex];
					foreach (var updateAfterType in ScriptBehaviourUpdateOrder.dependencyGraph[systemType].updateAfter)
					{
						systemView.updateAfter.Add(systemViewIndicesByType[updateAfterType]);
					}
				}
			}
		}
		
		[MenuItem("Window/Systems", false, 2017)]
		static void Open()
		{
			EditorWindow.GetWindow<SystemWindow>("Systems");
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
			Initialize();

			GUILayout.FlexibleSpace();
			
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
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
					var arrowDirection = systemViews[typeIndex].Center - systemView.Center;
					if (arrowDirection == Vector3.zero)
						continue;
					Handles.color = Color.black;
					var lineTexture = (Texture2D)EditorGUIUtility.LoadRequired("AALineRetina.png");
					var startPos = ExteriorPointFromOtherPoint(systemView.position, systemViews[typeIndex].Center);
					var endPos = ExteriorPointFromOtherPoint(systemViews[typeIndex].position, systemView.Center);
					endPos -= (endPos - startPos).normalized * 0.6f*kArrowSize;
					Handles.DrawAAPolyLine(lineTexture, EditorGUIUtility.pixelsPerPoint*kLineWidth, startPos, endPos);
					var rotation = Quaternion.LookRotation(arrowDirection, Vector3.forward);
					Handles.ConeHandleCap(0, endPos, rotation, kArrowSize, Event.current.type);
				}
			}

			BeginWindows();
			
			for (var i = 0; i < systemViews.Count; ++i)
			{
				var view = systemViews[i];
				view.position = GUILayout.Window(i, view.position, WindowFunction, "", GUI.skin.box);
			}
			
			EndWindows();
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
