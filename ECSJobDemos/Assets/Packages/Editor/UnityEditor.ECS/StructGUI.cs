using UnityEngine;
using Unity.Mathematics;
using UnityEngine.ECS;
using UnityEditor;
using System;
using System.Reflection;

public static class StructGUI {

	static GUIStyle labelStyle {
		get {
			if (m_LabelStyle == null)
			{
				m_LabelStyle = EditorStyles.miniLabel;
				m_LabelStyle.alignment = TextAnchor.MiddleRight;
			}
			return m_LabelStyle;
		}
	}
	static GUIStyle m_LabelStyle;

	public static void CellGUI(Rect rect, IComponentData data, int rows)
	{
		var fields = data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		var displayedFields = 0;
		foreach (var field in fields)
		{
			displayedFields += 1 + MathGUI.ColumnsForType(field.FieldType);
		}
		var fieldWidth = rect.width/displayedFields;
		var fieldHeight = rect.height/rows;
		var currentCell = new Rect(rect.x, rect.y, fieldWidth, fieldHeight);
		foreach (var field in fields)
		{
			GUI.Label(currentCell, field.Name, labelStyle);
			currentCell.x += fieldWidth;

			var value = field.GetValue(data);
			MathGUI.FieldGUI(currentCell, value);
			currentCell.x += fieldWidth * MathGUI.ColumnsForType(field.FieldType);
		}
	}

	public static int RowsForType(Type type)
	{
		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		var rows = 1;
		foreach (var field in fields)
		{
			rows = Mathf.Max(rows, MathGUI.RowsForType(field.FieldType));
		}
		return rows;
	}
}
