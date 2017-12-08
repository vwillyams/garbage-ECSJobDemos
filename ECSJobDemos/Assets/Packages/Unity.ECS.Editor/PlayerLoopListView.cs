using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Experimental.LowLevel;

public class PlayerLoopListView : TreeView
{
	
	public PlayerLoopListView(TreeViewState state) : base(state)
	{
		Reload();
	}

	protected override TreeViewItem BuildRoot()
	{
		var currentID = 0;
		TreeViewItem root;
		if (ScriptBehaviourUpdateOrder.LastPlayerLoopSystem.subSystemList == null ||
		    ScriptBehaviourUpdateOrder.LastPlayerLoopSystem.subSystemList.Length == 0)
		{
			root = new TreeViewItem {id = currentID++, depth = -1, displayName = "Root"};
			root.AddChild(new TreeViewItem {id = currentID++, displayName = "No Player Loop Loaded"});
		}
		else
		{
			root = AddCallsDepthFirst(ScriptBehaviourUpdateOrder.LastPlayerLoopSystem, ref currentID);
		}
		SetupDepthsFromParentsAndChildren(root);
		return root;
	}

	protected TreeViewItem AddCallsDepthFirst(PlayerLoopSystem system, ref int currentID)
	{
		var parent = new TreeViewItem
		{
			id = currentID++,
			depth = -1,
			displayName = system.type == null ? "null" : system.type.Name
		};
		if (system.subSystemList != null)
		{
			foreach (var subSystem in system.subSystemList)
				parent.AddChild(AddCallsDepthFirst(subSystem, ref currentID));
		}
		return parent;
	}
}
