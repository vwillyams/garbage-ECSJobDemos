

using UnityEditor;
using UnityEngine;

public static class EntityDebuggerStyles
{

    public static GUIStyle ComponentRequired => StyleAsset.styles[0];
    public static GUIStyle ComponentSubtractive => StyleAsset.styles[1];

    private static GUIStyleAsset StyleAsset
    {
        get
        {
            if (!styleAsset)
            {
                styleAsset = AssetDatabase.LoadAssetAtPath<GUIStyleAsset>("Packages/com.unity.entities/Unity.Entities.Editor/Resources/EntityDebuggerStyles.asset");
            }

            return styleAsset;
        }
    }
    
    private static GUIStyleAsset styleAsset;
}
