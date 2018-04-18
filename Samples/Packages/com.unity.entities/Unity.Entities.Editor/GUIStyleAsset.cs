using UnityEngine;

namespace Unity.Entities.Editor
{
    [CreateAssetMenu(fileName = "Styles.asset", menuName = "GUI Style Asset", order = 600)]
    public class GUIStyleAsset : ScriptableObject
    {

        public GUIStyle[] styles;
    }
}