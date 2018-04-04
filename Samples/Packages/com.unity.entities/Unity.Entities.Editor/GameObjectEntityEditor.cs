
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    [CustomEditor(typeof(GameObjectEntity))]
    public class GameObjectEntityEditor : UnityEditor.Editor
    {
        private readonly List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>> cachedMatches = new List<Tuple<ScriptBehaviourManager, List<ComponentGroup>>>();

        [SerializeField] private SystemInclusionList inclusionList;

        private void OnEnable()
        {
            inclusionList = new SystemInclusionList();
        }

        public override void OnInspectorGUI()
        {
            var gameObjectEntity = (GameObjectEntity) target;
            if (gameObjectEntity.EntityManager == null || !gameObjectEntity.EntityManager.IsCreated || !gameObjectEntity.EntityManager.Exists(gameObjectEntity.Entity))
                return;

            inclusionList.OnGUI(gameObjectEntity.EntityManager, gameObjectEntity.Entity);
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }
    }
}
