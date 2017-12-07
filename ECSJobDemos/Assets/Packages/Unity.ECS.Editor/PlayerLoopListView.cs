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
		var root  = new TreeViewItem { id = currentID++, depth = -1, displayName = "Root" };
		if (ScriptBehaviourUpdateOrder.LastPlayerLoopSystem.subSystemList == null ||
		    ScriptBehaviourUpdateOrder.LastPlayerLoopSystem.subSystemList.Length == 0)
		{
			root.AddChild(new TreeViewItem {id = currentID++, displayName = "No Player Loop Loaded"});
			SetupDepthsFromParentsAndChildren(root);
		}
		else
		{
			AddCallsDepthFirst(ScriptBehaviourUpdateOrder.LastPlayerLoopSystem, root, 0, ref currentID);
			SetupDepthsFromParentsAndChildren(root);
		}
		return root;
	}

	protected void AddCallsDepthFirst(PlayerLoopSystem system, TreeViewItem root, int depth, ref int currentID)
	{
		var child = new TreeViewItem
		{
			id = currentID++,
			depth = depth,
			displayName = system.type == null ? "null" : system.type.Name
		};
		root.AddChild(child);
		if (system.subSystemList != null)
		{
			foreach (var subSystem in system.subSystemList)
				AddCallsDepthFirst(subSystem, child, depth + 1, ref currentID);
		}
	}
}
