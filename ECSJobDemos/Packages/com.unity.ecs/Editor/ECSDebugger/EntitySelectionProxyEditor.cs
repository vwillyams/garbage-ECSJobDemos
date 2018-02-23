
using Unity.Properties;
using Unity.Properties.Entities;
using UnityEngine;
using UnityEditor;

namespace UnityEditor.ECS
{
    [CustomEditor(typeof(EntitySelectionProxy))]
    public class EntitySelectionProxyEditor : Editor
    {
        private EntityIMGUIVisitor visitor;
        
        void OnEnable()
        {
            visitor = new EntityIMGUIVisitor();
        }

        public override void OnInspectorGUI()
        {
            var targetProxy = (EntitySelectionProxy) target;
            targetProxy.container.PropertyBag.VisitStruct(ref targetProxy.container, visitor);
        }
    }
}
