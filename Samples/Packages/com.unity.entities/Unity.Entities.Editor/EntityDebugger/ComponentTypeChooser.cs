
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Tizen;

namespace Unity.Entities.Editor
{

    public enum ComponentMatchMode
    {
        Ignore,
        Require,
        Subtract
    }

    public interface IComponentTypeQueryWindow
    {
        void ComponentFilterChanged();
    }

    public class ComponentTypeChooser : EditorWindow, IComponentTypeQueryWindow
    {

        private static List<ComponentType> types;
        private static List<ComponentMatchMode> typeModes;

        private static ComponentTypeChooser chooserWindow;
        private static IComponentTypeQueryWindow callbackWindow;

        private static readonly Vector2 kDefaultSize = new Vector2(300f, 400f);

        public static void Open(Vector2 screenPosition, List<ComponentType> types, List<ComponentMatchMode> typeModes, IComponentTypeQueryWindow window)
        {
            callbackWindow = window;
            ComponentTypeChooser.types = types;
            ComponentTypeChooser.typeModes = typeModes;
            chooserWindow = GetWindowWithRect<ComponentTypeChooser>(new Rect(screenPosition, kDefaultSize), true, "Choose Component", true);
        }

        private ComponentTypeListView typeListView;

        private void OnEnable()
        {
            typeListView = new ComponentTypeListView(new TreeViewState(), types, typeModes, this);
        }

        public void ComponentFilterChanged()
        {
            callbackWindow.ComponentFilterChanged();
        }

        private void OnGUI()
        {
            typeListView.OnGUI(new Rect(Vector2.zero, position.size));
        }

        private void OnLostFocus()
        {
            Close();
        }
    }
}
