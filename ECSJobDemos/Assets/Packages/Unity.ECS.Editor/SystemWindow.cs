using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.ECS;

namespace UnityEditor.ECS
{

	public class SystemWindow : EditorWindow
	{
		private readonly Rect kStartPosition = new Rect(0f, 0f, 100f, 30f);
		
		[System.Serializable]
		public class SystemViewData
		{
			public string name;
			public Rect position;

			public SystemViewData(string name, Rect position)
			{
				this.name = name;
				this.position = position;
			}
		}

		[SerializeField] private List<SystemViewData> systemViews;

		private ComponentSystem[] systems {
			get {
				if (DependencyManager.Root == null)
					return new ComponentSystem[0];
				return  (from s in DependencyManager.Root.BehaviourManagers
					where s is ComponentSystem
					select s as ComponentSystem).ToArray();
			}
		}

		void Initialize()
		{
			// here we need to collect dependencies (so far only manager dependencies, UpdateAfterAttribute)
			var systemNames = (from s in systems select s.ToString()).ToArray();
			foreach (var name in systemNames)
			{
				if (systemViews.FindIndex(x => x.name == name) >= 0)
				{
					
				}
				else
				{
					systemViews.Add(new SystemViewData(name, kStartPosition));
				}
			}
		}
		
		[SerializeField]
		
		[MenuItem("Window/Systems", false, 2017)]
		static void Open()
		{
			EditorWindow.GetWindow<SystemWindow>("Systems");
		}

		void OnGUI()
		{
			Initialize();
			
			BeginWindows();

			for (var i = 0; i < systemViews.Count; ++i)
			{
				var view = systemViews[i];
				view.position = GUILayout.Window(i, view.position, WindowFunction, view.name);
			}
			
			EndWindows();
		}

		void WindowFunction(int id)
		{
			GUI.DragWindow(new Rect(0f, 0f, 1000f, 1000f));
			GUILayout.Label(systemViews[id].name);
		}
		
		
	}
	
}
