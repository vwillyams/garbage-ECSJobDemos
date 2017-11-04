using UnityEngine;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEditor;
using System;
using System.Reflection;

public static class StructGUI {

	public static void CellGUI(Rect rect, IComponentData data)
	{
		var fields = data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		var displayedFields = 0;
		var rows = 1;
		foreach (var field in fields)
		{
			displayedFields += ColumnsForField(field.FieldType);
			rows = Mathf.Max(rows, RowsForType(data.GetType()));
		}
		var fieldWidth = rect.width/displayedFields;
		var fieldHeight = rect.height/rows;
		var fieldOffset = 0;
		foreach (var field in fields)
		{
			if (field.FieldType == typeof(float3))
			{
				var f3 = (float3)field.GetValue(data);
				var v3 = new Vector3(f3.x, f3.y, f3.z);
				EditorGUI.Vector3Field(new Rect(rect.x + fieldOffset*fieldWidth, rect.y, fieldWidth * 3, fieldHeight), GUIContent.none, v3);
				fieldOffset += 3;
			}
			else if (field.FieldType == typeof(float4x4))
			{
				var f4x4 = (float4x4)field.GetValue(data);
				var v4 = new Vector4(f4x4.m0.x, f4x4.m0.y, f4x4.m0.z, f4x4.m0.w);
				EditorGUI.Vector4Field(new Rect(rect.x + fieldOffset*fieldWidth, rect.y + fieldHeight * 0, fieldWidth * 3, fieldHeight), GUIContent.none, v4);
				v4 = new Vector4(f4x4.m1.x, f4x4.m1.y, f4x4.m1.z, f4x4.m1.w);
				EditorGUI.Vector4Field(new Rect(rect.x + fieldOffset*fieldWidth, rect.y + fieldHeight * 1, fieldWidth * 3, fieldHeight), GUIContent.none, v4);
				v4 = new Vector4(f4x4.m2.x, f4x4.m2.y, f4x4.m2.z, f4x4.m2.w);
				EditorGUI.Vector4Field(new Rect(rect.x + fieldOffset*fieldWidth, rect.y + fieldHeight * 2, fieldWidth * 3, fieldHeight), GUIContent.none, v4);
				v4 = new Vector4(f4x4.m3.x, f4x4.m3.y, f4x4.m3.z, f4x4.m3.w);
				EditorGUI.Vector4Field(new Rect(rect.x + fieldOffset*fieldWidth, rect.y + fieldHeight * 3, fieldWidth * 3, fieldHeight), GUIContent.none, v4);
			}
		}
	}

	public static int ColumnsForField(Type type)
	{
		if (type == typeof(float4x4))
			return 4;
		else if (type == typeof(float3))
			return 3;
		else
			return 1;
	}

	public static int RowsForType(Type type)
	{
		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		var rows = 1;
		foreach (var field in fields)
		{
			if (field.FieldType == typeof(float4x4))
				rows = Mathf.Max(rows, 4);
		}
		return rows;
	}
}
