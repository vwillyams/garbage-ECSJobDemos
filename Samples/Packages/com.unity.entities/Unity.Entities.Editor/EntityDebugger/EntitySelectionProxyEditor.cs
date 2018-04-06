using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    [CustomEditor(typeof(EntitySelectionProxy))]
    public class EntitySelectionProxyEditor : UnityEditor.Editor
    {
        private EntityIMGUIVisitor visitor;

        private readonly List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>> cachedMatches = new List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>>();

        [SerializeField] private SystemInclusionList inclusionList;
        
        void OnEnable()
        {
            visitor = new EntityIMGUIVisitor();
            inclusionList = new SystemInclusionList();
        }

        public override void OnInspectorGUI()
        {
            var targetProxy = (EntitySelectionProxy) target;
            if (!targetProxy.Exists)
                return;
            var container = targetProxy.Container;
            targetProxy.Container.PropertyBag.Visit(ref container, visitor);

            GUI.enabled = true;
            
            inclusionList.OnGUI(targetProxy.EntityManager, targetProxy.Entity);
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }
    }
}
