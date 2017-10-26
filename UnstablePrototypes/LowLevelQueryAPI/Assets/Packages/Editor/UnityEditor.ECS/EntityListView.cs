using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine.Jobs;
using System.Linq;
using System.Text;

namespace UnityEngine.ECS
{
    public class EntityListView : TreeView {

        TupleSystem currentSystem;

        List<object> nativeArrays;

        List<int> typeIndexToNativeIndex;

        EntityWindow window;

        public EntityListView(TreeViewState state, MultiColumnHeader header, TupleSystem system, EntityWindow window) : base(state, header)
        {
            this.window = window;
            this.currentSystem = system;
            // header.sortingChanged += OnSortChanged;
            Reload();
        }

        public static MultiColumnHeaderState BuildHeaderState(TupleSystem system)
        {
            var types = system.EntityGroup.Types;
            var columns = new List<MultiColumnHeaderState.Column>(types.Length + 1);

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Index"),
                contextMenuText = "Asset",
                headerTextAlignment = TextAlignment.Center,
                sortedAscending = true,
                sortingArrowAlignment = TextAlignment.Right,
                width = 30, 
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
					width = 60, 
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
                nativeArrays = new List<object>();
                typeIndexToNativeIndex = new List<int>();
                var columnIndex = 0;
                foreach (var type in currentSystem.EntityGroup.Types)
                {
                    if (type.GetInterfaces().Contains(typeof(IComponentData)))
                    {
                        var attr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
                        var method = typeof(EntityGroup).GetMethod("GetComponentDataArray", attr);
                        method = method.MakeGenericMethod(type);
                        var args = new object[] {false};
                        var array = method.Invoke(currentSystem.EntityGroup, args);
                        nativeArrays.Add(array);
                        typeIndexToNativeIndex.Add(columnIndex);
                    }
                    else
                    {
                        typeIndexToNativeIndex.Add(-1);
                    }
                    ++columnIndex;
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
			// Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
			CenterRectUsingSingleLineHeight(ref cellRect);

			if (column == 0)
            {
                DefaultGUI.LabelRightAligned(cellRect, args.item.displayName, args.selected, args.focused);
            }
            else
            {
                var type = currentSystem.EntityGroup.Types[column - 1];
                var nativeIndex = typeIndexToNativeIndex[column - 1];
                if (nativeIndex < 0)
                    return;
                var array = nativeArrays[nativeIndex];
                var arrayType = typeof(ComponentDataArray<>).MakeGenericType(type);
                var arrayIndexer = arrayType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();
                var arrayElement = arrayIndexer.Invoke(array, new object[]{item.id});
                
                StructGUI.CellGUI(cellRect, (IComponentData)arrayElement);
            }
        }

    }
}