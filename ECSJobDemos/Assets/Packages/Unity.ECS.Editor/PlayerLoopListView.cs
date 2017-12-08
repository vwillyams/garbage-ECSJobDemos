using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ECS;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Experimental.LowLevel;

public class PlayerLoopListView : TreeView
{

	protected HashSet<string> systemNames;
	protected Dictionary<int, PlayerLoopSystem> playerLoopSystemsByListID;
	protected HashSet<int> systemSubtreeIDs;
	
	public PlayerLoopListView(TreeViewState state, HashSet<string> systemNames) : base(state)
	{
		this.systemNames = systemNames;
		Reload();
	}

	protected override TreeViewItem BuildRoot()
	{
		var currentID = 0;
		TreeViewItem root;
		playerLoopSystemsByListID = new Dictionary<int, PlayerLoopSystem>();
		systemSubtreeIDs = new HashSet<int>();
		if (ScriptBehaviourUpdateOrder.LastPlayerLoopSystem.subSystemList == null ||
		    ScriptBehaviourUpdateOrder.LastPlayerLoopSystem.subSystemList.Length == 0)
		{
			root = new TreeViewItem {id = currentID++, depth = -1, displayName = "Root"};
			root.AddChild(new TreeViewItem {id = currentID++, displayName = "No Player Loop Loaded"});
		}
		else
		{
			bool dummy;
			AddCallsDepthFirst(ScriptBehaviourUpdateOrder.LastPlayerLoopSystem, ref currentID, out root, out dummy);
		}
		SetupDepthsFromParentsAndChildren(root);
		return root;
	}

	protected void AddCallsDepthFirst(PlayerLoopSystem system, ref int currentID, out TreeViewItem parent, out bool hasSystems)
	{
		parent = new TreeViewItem
		{
			id = currentID++,
			depth = -1,
			displayName = system.type == null ? "null" : system.type.Name
		};
		playerLoopSystemsByListID.Add(parent.id, system);
		
		hasSystems = system.type != null && systemNames.Contains(system.type.FullName);
		if (system.subSystemList != null)
		{
			foreach (var subSystem in system.subSystemList)
			{
				TreeViewItem child;
				bool childHasSystems;
				AddCallsDepthFirst(subSystem, ref currentID, out child, out childHasSystems);
				parent.AddChild(child);
				hasSystems |= childHasSystems;
			}
		}
		if (hasSystems && !systemSubtreeIDs.Contains(parent.id))
		{
			systemSubtreeIDs.Add(parent.id);
		}
	}

	protected override void RowGUI(RowGUIArgs args)
	{
		GUI.enabled = systemSubtreeIDs.Contains(args.item.id);
		base.RowGUI(args);
		GUI.enabled = true;
	}
}
