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
			displayedFields += MathGUI.ColumnsForType(field.FieldType);
		}
		rows = RowsForType(data.GetType());
		var fieldWidth = rect.width/displayedFields;
		var fieldHeight = rect.height/rows;
		var currentCell = new Rect(rect.x, rect.y, fieldWidth, fieldHeight);
		foreach (var field in fields)
		{
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
