
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.Entities.Editor
{

    public interface IComponentTypeQueryWindow
    {
        void ComponentFilterChanged();
    }

    public class ComponentTypeChooser : EditorWindow, IComponentTypeQueryWindow
    {

        private static List<ComponentType> types;
        private static List<bool> typeSelections;

        private static ComponentTypeChooser chooserWindow;
        private static IComponentTypeQueryWindow callbackWindow;

        private static readonly Vector2 kDefaultSize = new Vector2(300f, 400f);

        public static void Open(Vector2 screenPosition, List<ComponentType> types, List<bool> typeSelections, IComponentTypeQueryWindow window)
        {
            callbackWindow = window;
            ComponentTypeChooser.types = types;
            ComponentTypeChooser.typeSelections = typeSelections;
            chooserWindow = GetWindowWithRect<ComponentTypeChooser>(new Rect(screenPosition, kDefaultSize), true, "Choose Component", true);
        }

        private SearchField searchField;
        private ComponentTypeListView typeListView;
        private readonly string kSearchFieldName = "_ComponentTypeChooser SearchField";

        private void OnEnable()
        {
            searchField = new SearchField();
            searchField.SetFocus();
            typeListView = new ComponentTypeListView(new TreeViewState(), types, typeSelections, this);
        }

        public void ComponentFilterChanged()
        {
            callbackWindow.ComponentFilterChanged();
        }

        private void OnGUI()
        {
            typeListView.searchString = searchField.OnGUI(typeListView.searchString, GUILayout.Height(20f), GUILayout.ExpandWidth(true));
            typeListView.OnGUI(GUIHelpers.GetExpandingRect());
        }

        private void OnLostFocus()
        {
            Close();
        }
    }
}
