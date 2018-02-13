using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.Properties.Editor
{
    public class BindingsHelper : EditorWindow
    {
        private SerializedObject _serialized;
        private SerializedObjectContainer _container;
        private UnityEngine.Object _target;
        private Vector2 _scrollPos;
        
        [MenuItem("Window/Bindings Helper")]
        private static void Init()
        {
            var window = GetWindow<BindingsHelper>();
            window.titleContent = new GUIContent("Bindings Helper");
            window.Show();
        }

        private void InvalidateTarget()
        {
            _container = null;
            if (_target)
            {
                _serialized = new SerializedObject(_target);
                _container = new SerializedObjectContainer(_serialized);
            }
        }

        private class Visitor : IPropertyVisitor
        {
            private int _inList;
            
            public bool Visit<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context) where TContainer : IPropertyContainer
            {
                var p = (IBaseSerializedProperty) context.Property;
                if (_inList == 0)
                {
                    EditorGUILayout.LabelField(p.Property.propertyPath);
                }
                else
                {
                    EditorGUILayout.LabelField(p.Property.propertyPath + " at index " + context.Index);   
                }

                return true;
            }

            public bool VisitEnum<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context)
                where TContainer : IPropertyContainer
                where TValue : struct
            {
                throw new System.NotImplementedException();
            }

            public bool BeginSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
            {
                var p = (IBaseSerializedProperty) context.Property;
                EditorGUILayout.LabelField(p.Property.propertyPath);
                return true;
            }

            public void EndSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
            {
            }

            public bool BeginList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
            {
                var p = (IBaseSerializedProperty) context.Property;
                EditorGUILayout.LabelField(p.Property.propertyPath);
                ++_inList;
                return true;
            }

            public void EndList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
            {
                --_inList;
            }
        }
        private readonly Visitor _visitor = new Visitor();


        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _target = EditorGUILayout.ObjectField("Target", _target, typeof(UnityEngine.Object), true);
            if (EditorGUI.EndChangeCheck() || _container == null)
            {
                InvalidateTarget();
            }
            if (_target == null)
            {
                EditorGUILayout.LabelField("Drag an object to dispay its binding information");
                return;
            }
            
            _serialized.Update();
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            try
            {
                _container.PropertyBag.Visit(ref _container, _visitor);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }
    }
}