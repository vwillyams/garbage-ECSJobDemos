using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;
using UnityEditor;
using System.Reflection;

public static class StructGUI {

	public static void CellGUI(Rect rect, IComponentData data)
	{
		var fields = data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		var displayedFields = 0;
		foreach (var field in fields)
		{
			if (field.FieldType == typeof(float3))
				displayedFields += 3;
		}
		var fieldWidth = rect.width/displayedFields;
		var fieldOffset = 0;
		foreach (var field in fields)
		{
			if (field.FieldType == typeof(float3))
			{
				var f3 = (float3)field.GetValue(data);
				var v3 = new Vector3(f3.x, f3.y, f3.z);
				EditorGUI.Vector3Field(new Rect(rect.x + fieldOffset*fieldWidth, rect.y, fieldWidth * 3, rect.height), GUIContent.none, v3);
				fieldOffset += 3;
			}
		}
	}


}
