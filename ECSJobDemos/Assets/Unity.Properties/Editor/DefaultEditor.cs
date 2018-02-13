using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Properties.Editor
{
    public class DefaultEditor : UnityEditor.Editor
    {
        private SerializedObjectContainer _container;
        private readonly Visitor _visitor = new Visitor();

        private void OnEnable()
        {
            _visitor.Editor = this;
            _container = new SerializedObjectContainer(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            OnInspectorGUIHeader();
            _container.PropertyBag.Visit(ref _container, _visitor);
            OnInspectorGUIFooter();
            serializedObject.ApplyModifiedProperties();
        }
        
        protected virtual void OnInspectorGUIHeader()
        {}

        protected virtual void OnPropertyGUI(IPropertyContainer container, IBaseSerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property.Property, true);
            if (EditorGUI.EndChangeCheck())
            {
                container.VersionStorage?.IncrementVersion(property, ref container);
            }
        }
        
        protected virtual void OnInspectorGUIFooter()
        {}
        
        private class Visitor : IPropertyVisitor
        {
            public DefaultEditor Editor { get; set; }
            
            public bool Visit<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context)
                where TContainer : IPropertyContainer
            {
                // generic case: use the PropertyField and manage versioning manually
                var serProp = context.Property as IBaseSerializedProperty;
                Assert.IsNotNull(serProp);

                GUI.enabled = serProp.Property.name != "m_Script";
                Editor.OnPropertyGUI(container, serProp);

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
                var serProp = context.Property as IBaseSerializedProperty;
                Assert.IsNotNull(serProp);
                Editor.OnPropertyGUI(container, serProp);
                return false;
            }

            public void EndSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
            {
            }

            public bool BeginList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
            {
                var serProp = context.Property as IBaseSerializedProperty;
                Assert.IsNotNull(serProp);
                Editor.OnPropertyGUI(container, serProp);
                // OnPropertyGUI takes care of the list items
                return false;
            }

            public void EndList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
            {
            }
        }
    }

    
}