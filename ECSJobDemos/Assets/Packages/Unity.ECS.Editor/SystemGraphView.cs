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
	public class SystemGraphView
	{
		private readonly Rect kStartPosition = new Rect(0f, 0f, 1f, 1f);
		public const float kArrowSize = 11f;
		private const float kLineWidth = 2f;
		private const float kLayerHeight = 50f;
		private const float kHorizontalSpacing = 200f;
		

		private List<SystemViewData> systemViews;

		public void SetSystems(Type[] systemTypes, ref List<SystemViewData> systemViews)
		{
			if (systemViews == null)
				systemViews = new List<SystemViewData>();
		    this.systemViews = systemViews;
			
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
		}

		public void GraphLayout()
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

		public void OnGUIArrows()
		{
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
		}

	    public void OnGUIWindows()
	    {
	        for (var i = 0; i < systemViews.Count; ++i)
	        {
	            var view = systemViews[i];
	            view.position = GUILayout.Window(i, view.position, WindowFunction, "", GUI.skin.box);
	        }
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
