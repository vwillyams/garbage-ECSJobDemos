using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

public class CustomLayoutAlgorithm
{
    public bool leftToRight { get; set; }
    public GraphView graphView { get; private set; }
    public float xSpacing { get; set; }
    public float ySpacing { get; set; }

    public class LayoutNodeItem
    {
        public List<LayoutNodeItem> parents { get; private set; }
        public List<LayoutNodeItem> children { get; private set; }
        public bool visited { get; set; }
        public Node node { get; set; }

        public LayoutNodeItem(Node node = null)
        {
            this.node = node;
            visited = false;
            parents = new List<LayoutNodeItem>();
            children = new List<LayoutNodeItem>();
        }

        public bool IsDescendantOf(LayoutNodeItem item)
        {
            if (parents.Count == 0)
                return false;

            foreach (var parent in parents)
            {
                if (parent == item)
                {
                    return true;
                }
                else
                {
                    if (parent.IsDescendantOf(item))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void Add(LayoutNodeItem item)
        {
            if (item.parents.Contains(this) || IsDescendantOf(item))
            {
                return;
            }

            item.parents.Add(this);
            children.Add(item);
        }
    }

    public CustomLayoutAlgorithm(GraphView graphView)
    {
        this.graphView = graphView;
        leftToRight = true;
        xSpacing = 100;
        ySpacing = 10;
    }

    public void AutoLayoutAll()
    {
        List<Node> nodes = graphView.nodes.ToList();

        LayoutNodes(nodes);
    }

    public void AutoLayoutSelection()
    {
        var node = graphView.selection[0] as Node;
        List<Node> nodes = new List<Node>();

        nodes.Add(node);

        LayoutNodes(nodes, node);
    }

    private void BuildLayoutNodeTree(LayoutNodeItem item, Node node, List<Node> visitedNodes, Dictionary<Node, LayoutNodeItem> nodeItemMap)
    {
        // If a tree has been already built for the node then there is no need to go any further
        if (visitedNodes.Contains(node))
        {
            return;
        }

        visitedNodes.Add(node);

        // Collect all input or output ports of the node depending on the direction
        List<Port> ports = leftToRight ? node.outputContainer.Children().OfType<Port>().ToList() : node.inputContainer.Children().OfType<Port>().ToList();
        //List<Port> ports = node.inputContainer.Children().OfType<Port>().Union(node.outputContainer.Children().OfType<Port>()).ToList();
        List<Node> connectedNodes = new List<Node>();

        foreach (Port port in ports)
        {
            var connectedPorts = new List<Port>();
            IEnumerable<Edge> edges = port.connections;

            foreach (var edge in edges)
            {
                var otherPort = port.direction == Direction.Input ? edge.output : edge.input;
                var otherNode = otherPort.GetFirstOfType<Node>();

                // Try to find the item associated with the node in the node item map before creating it
                LayoutNodeItem otherNodeItem;

                if (nodeItemMap.TryGetValue(otherNode, out otherNodeItem) == false)
                {
                    otherNodeItem = new LayoutNodeItem(otherNode);
                    nodeItemMap[otherNode] = otherNodeItem;
                }

                if (leftToRight)
                {
                    // If the connected item is the output then add it as a child item to the current item otherwise set it as its parent
                    if (port.direction == Direction.Output)
                    {

                        item.Add(otherNodeItem);
                    }
                    else
                    {
                        otherNodeItem.Add(item);
                    }
                }
                else
                {
                    // If the connected item is the output then add it as a child item to the current item otherwise set it as its parent
                    if (port.direction == Direction.Input)
                    {

                        item.Add(otherNodeItem);
                    }
                    else
                    {
                        otherNodeItem.Add(item);
                    }
                }
            }
        }
    }

    void LayoutNodeTreeRecursively(LayoutNodeItem item, ref float childrenTop, ref float childrenBottom, ref float childrenMaxBottom)
    {
        bool isRoot = item.node == null;
        float nextX = 0
            , nextRight = 0
            , nextY = 0
            , right = 0
            , top = 0
            , bottom = 0;

        // If the current item is the root item then the next position is at origin (0, 0); otherwise the next x position is to the right if leftToRight or to the left if !leftToRight of the current item plus some horizontal spacing
        if (isRoot == false)
        {
            Vector2 pos = item.node.GetPosition().position;

            top = pos.y;

            if (leftToRight)
            {
                nextX = pos.x + item.node.GetPosition().width + xSpacing;
            }
            else
            {
                nextRight = pos.x - xSpacing;
            }

            bottom = top + item.node.GetPosition().height;
            nextY = top;
            childrenTop = top;
        }

        int index = 0;

        // Go through the child items of the current item
        foreach (LayoutNodeItem childItem in item.children)
        {
            // If the current child has already been laid out then skip it
            if (childItem.visited == false)
            {
                float actualYSpacing = ySpacing;

                // Do not add vertical spacing to the first child item to lay out. It has the same y position as the current item
                if (index == 0)
                {
                    actualYSpacing = 0;
                }

                Rect newChildGeom = childItem.node.GetPosition();

                if (leftToRight)
                {
                    newChildGeom.position = new Vector2(nextX, nextY + actualYSpacing);
                }
                else
                {
                    newChildGeom.position = new Vector2(nextRight - newChildGeom.width, nextY + actualYSpacing);
                }

                childItem.node.SetPosition(newChildGeom);
                childItem.visited = true;

                float childrenRectTop = 0;
                float childrenRectBottom = 0;

                // Lays out the child item recursively and retrieves the next y position of its sibling
                LayoutNodeTreeRecursively(childItem, ref childrenRectTop, ref childrenRectBottom, ref nextY);

                // If the current child item is a top-level item then center it on the bounding rect that contains its own laid out child items
                float center = (childrenRectTop + childrenRectBottom) / 2;
                var childGeom = childItem.node.GetPosition();

                childGeom.y = center - (childGeom.height / 2);
                childItem.node.SetPosition(childGeom);

                if (index == 0)
                {
                    childrenTop = childGeom.y;
                }

                childrenBottom = childGeom.y + childGeom.height;
                index++;
            }
        }

        childrenBottom = Math.Max(bottom, childrenBottom);
        childrenMaxBottom = Math.Max(nextY, bottom);
    }

    private void LayoutNodes(List<Node> nodes, Node referenceNode = null)
    {
        List<Node> visitedNodes = new List<Node>();
        Dictionary<Node, LayoutNodeItem> nodeItemMap = new Dictionary<Node, LayoutNodeItem>();
        LayoutNodeItem root = new LayoutNodeItem();

        // First build the tree of connected items
        foreach (Node node in nodes)
        {
            if (visitedNodes.Contains(node) == false)
            {
                LayoutNodeItem item;

                if (nodeItemMap.TryGetValue(node, out item) == false)
                {
                    item = new LayoutNodeItem(node);
                    nodeItemMap[node] = item;
                }
                BuildLayoutNodeTree(item, node, visitedNodes, nodeItemMap);
            }
        }

        // Add the top level items (not used as input) to the root item
        foreach (var item in nodeItemMap)
        {
            if (item.Value.parents.Count == 0)
            {
                root.Add(item.Value);
            }
        }

        DoLayout(root, referenceNode);
    }

    public virtual void DoLayout(LayoutNodeItem root, Node referenceNode)
    { 
        Vector2 oldRefPos = Vector2.zero;
        float childrenRectTop = 0, childrenRectBottom = 0, childrenRectMaxBottom = 0;

        LayoutNodeTreeRecursively(root, ref childrenRectTop, ref childrenRectBottom, ref childrenRectMaxBottom);
    }
}
