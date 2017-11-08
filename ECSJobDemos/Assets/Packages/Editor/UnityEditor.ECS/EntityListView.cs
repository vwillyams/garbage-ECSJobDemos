using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;
using System.Reflection;
using Unity.Jobs;
using System.Linq;
using System.Text;

namespace UnityEngine.ECS
{
    public class EntityListView : TreeView {

        ComponentGroup currentSystem;

        Dictionary<Type, object> nativeArrays;

        int linesPerRow;
        const float pointsBetweenRows = 2f;

        const float indexWidth = 30f;

        public EntityListView(TreeViewState state, MultiColumnHeader header, ComponentGroup system) : base(state, header)
        {
            this.currentSystem = system;
            // header.sortingChanged += OnSortChanged;
            Reload();
        }

        public static MultiColumnHeaderState GetOrBuildHeaderState(ref List<MultiColumnHeaderState> headerStates, ComponentGroup system, float listWidth)
        {
            if (headerStates == null)
                headerStates = new List<MultiColumnHeaderState>();
            
            var types = system.Types;

            foreach (var headerState in headerStates)
            {
                var match = true;
                for (var i = 1; i < types.Length; ++i)
                {
                    if (headerState.columns[i + 1].headerContent.text != types[i].Name)
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return headerState;
            }

            var newHeaderState = BuildHeaderState(system, listWidth);
            headerStates.Add(newHeaderState);
            return newHeaderState;
        }

        static MultiColumnHeaderState BuildHeaderState(ComponentGroup system, float listWidth)
        {
            var types = system.Types;
            var columns = new List<MultiColumnHeaderState.Column>(types.Length + 1);

            var cells = new int[types.Length];

            var totalCells = 0;
            for (var i = 0; i < types.Length; ++i)
            {
                cells[i] = StructGUI.ColumnsForType(types[i]);
                totalCells += cells[i];
            }

            var cellWidth = listWidth - indexWidth;
            if (totalCells > 0f)
                cellWidth /= totalCells;

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Index"),
                contextMenuText = "Asset",
                headerTextAlignment = TextAlignment.Center,
                sortedAscending = true,
                sortingArrowAlignment = TextAlignment.Right,
                width = indexWidth, 
                minWidth = 30,
                maxWidth = 60,
                autoResize = false,
                allowToggleVisibility = true
            });
            for (var i = 0; i < types.Length; ++i)
            {
                columns.Add(new MultiColumnHeaderState.Column
                {
					headerContent = new GUIContent(types[i].Name),
					contextMenuText = "Asset",
					headerTextAlignment = TextAlignment.Center,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Right,
					width = cells[i] * cellWidth, 
					minWidth = 60,
					maxWidth = 500,
					autoResize = false,
					allowToggleVisibility = true
                });
            }

            var headerState = new MultiColumnHeaderState(columns.ToArray());

            return headerState;
        }

        public void PrepareData()
        {
            if (currentSystem != null)
            {
                nativeArrays = new Dictionary<Type, object>();
                linesPerRow = 1;
                foreach (var type in currentSystem.Types)
                {
                    if (type.GetInterfaces().Contains(typeof(IComponentData)))
                    {
                        var attr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
                        linesPerRow = Mathf.Max(linesPerRow, StructGUI.RowsForType(type));
                        rowHeight = StructGUI.pointsPerLine * linesPerRow + pointsBetweenRows;
                        var method = typeof(ComponentGroup).GetMethod("GetComponentDataArray", attr);
                        method = method.MakeGenericMethod(type);
                        var args = new object[] {true};
                        var array = method.Invoke(currentSystem, args);
                        nativeArrays.Add(type, array);
                    }
                }
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            var root  = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
            if (currentSystem == null || currentSystem.GetEntityArray().Length == 0)
            {
                root.AddChild(new TreeViewItem { id = 0, depth = -1 });
            }
            else
            {
                for (var entityIndex = 0; entityIndex < currentSystem.GetEntityArray().Length; ++entityIndex)
                {
                    root.AddChild(new TreeViewItem { id = entityIndex, displayName = entityIndex.ToString()});
                }
                SetupDepthsFromParentsAndChildren(root);
            }
            return root;
        }

		protected override void RowGUI (RowGUIArgs args)
		{
            if (args.item.depth == -1)
                return;
			var item = args.item;

			for (int i = 0; i < args.GetNumVisibleColumns (); ++i)
			{
				CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
			}
		}

		void CellGUI (Rect cellRect, TreeViewItem item, int column, ref RowGUIArgs args)
		{
			if (column == 0)
            {
                DefaultGUI.LabelRightAligned(cellRect, args.item.displayName, args.selected, args.focused);
            }
            else
            {
                var typeIndex = column - 1;
                var type = currentSystem.Types[typeIndex];
                if (!nativeArrays.ContainsKey(type))
                    return;
                var array = nativeArrays[type];
                var arrayType = typeof(ComponentDataArray<>).MakeGenericType(type);
                var arrayIndexer = arrayType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();
                var arrayElement = arrayIndexer.Invoke(array, new object[]{item.id});
                
                cellRect.height -= pointsBetweenRows;
                StructGUI.CellGUI(cellRect, (IComponentData)arrayElement, linesPerRow);
            }
        }

    }
}