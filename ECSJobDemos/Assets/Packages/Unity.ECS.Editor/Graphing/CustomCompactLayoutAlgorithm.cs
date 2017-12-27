using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

public class CustomCompactLayoutAlgorithm : CustomLayoutAlgorithm
{
    public bool shouldUpdatePositions { get; set; }

    readonly IList<Layer> layers = new List<Layer>();
    readonly IDictionary<LayoutNodeItem, VertexData> data = new Dictionary<LayoutNodeItem, VertexData>();
    public Dictionary<GraphElement, Vector2> VertexPositions = new Dictionary<GraphElement, Vector2>();

    class Layer
    {
        public float Size;
        public float NextPosition;
        public readonly IList<LayoutNodeItem> Vertices = new List<LayoutNodeItem>();
        public float LastTranslate;

        public Layer()
        {
            LastTranslate = 0;
        }
        /* Width and Height Optimization */
    }


    class VertexData
    {
        public LayoutNodeItem parent;
        public float translate;
        public float position;

        /* Width and Height Optimization */
    }

    public CustomCompactLayoutAlgorithm(GraphView graphView) : base(graphView)
    {
        shouldUpdatePositions = false;
        xSpacing = 100;
        ySpacing = 10;
    }

    protected float CalculatePosition(LayoutNodeItem v, LayoutNodeItem parent, int l)
    {
        if (v.visited)
            return -1; //this node is already layed out

        while (l >= layers.Count)
            layers.Add(new Layer());

        var layer = layers[l];
        var size = new Vector2(v.node.GetPosition().size.y, v.node.GetPosition().size.x);
        var d = new VertexData { parent = parent };
        data[v] = d;

        layer.NextPosition += size.x / 2.0f;
        if (l > 0)
        {
            layer.NextPosition += layers[l - 1].LastTranslate;
            layers[l - 1].LastTranslate = 0;
        }
        layer.Size = Math.Max(layer.Size, size.y + xSpacing);
        layer.Vertices.Add(v);
        if (v.children.Count == 0/*spanningTree.OutDegree(v) == 0*/)
        {
            d.position = layer.NextPosition;
        }
        else
        {
            float minPos = float.MaxValue;
            float maxPos = -float.MaxValue;
            //first put the children
            foreach (var child in v.children/*spanningTree.OutEdges(v).Select(e => e.Target)*/)
            {
                float childPos = CalculatePosition(child, v, l + 1);
                if (childPos >= 0)
                {
                    minPos = Math.Min(minPos, childPos);
                    maxPos = Math.Max(maxPos, childPos);
                }
            }
            if (minPos != double.MaxValue)
                d.position = (minPos + maxPos) / 2.0f;
            else
                d.position = layer.NextPosition;
            d.translate = Math.Max(layer.NextPosition - d.position, 0);

            layer.LastTranslate = d.translate;
            d.position += d.translate;
            layer.NextPosition = d.position;
        }
        layer.NextPosition += size.x / 2.0f + ySpacing /* Parameters.VertexGap*/;

        return d.position;
    }

    protected void AssignPositions()
    {
        float layerSize = 0;
        bool changeCoordinates = true/* (Parameters.Direction == LayoutDirection.LeftToRight || Parameters.Direction == LayoutDirection.RightToLeft)*/;
        float direction = leftToRight ? 1 : -1;

        foreach (var layer in layers)
        {
            foreach (var v in layer.Vertices)
            {
                var size = new Vector2(v.node.GetPosition().size.y, v.node.GetPosition().size.x);


                var d = data[v];
                if (d.parent != null)
                {
                    d.position += data[d.parent].translate;
                    d.translate += data[d.parent].translate;
                }

                VertexPositions[v.node] = changeCoordinates ? new Vector2(direction * (layerSize + size.y / 2.0f), d.position)
                    : new Vector2(d.position, direction * (layerSize + size.y / 2.0f));
            }
            layerSize += layer.Size;
        }

        if (direction < 0)
            NormalizePositions();
    }

    protected virtual void NormalizePositions()
    {
        NormalizePositions(VertexPositions);
    }

    protected static void NormalizePositions(IDictionary<GraphElement, Vector2> vertexPositions)
    {
        if (vertexPositions == null || vertexPositions.Count == 0)
            return;

        //get the topLeft position
        var topLeft = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        foreach (var pos in vertexPositions.Values.ToArray())
        {
            topLeft.x = Math.Min(topLeft.x, pos.x);
            topLeft.y = Math.Min(topLeft.y, pos.y);
        }

        //translate with the topLeft position
        foreach (var v in vertexPositions.Keys.ToArray())
        {
            var pos = vertexPositions[v];
            pos.x -= topLeft.x;
            pos.y -= topLeft.y;
            vertexPositions[v] = pos;
        }
    }

    public override void DoLayout(LayoutNodeItem root, Node referenceNode)
    {
        //then the others
        foreach (var source in root.children)
            CalculatePosition(source, null, 0);

        AssignPositions();

        if (referenceNode != null)
        {
            // If there is an node used as reference then the layout will be performed so that this reference node is not moved
            var delta = VertexPositions[referenceNode] - referenceNode.GetPosition().center;
            List<GraphElement> keys = new List<GraphElement>(VertexPositions.Keys);

            foreach (var key in keys)
            {
                var value = VertexPositions[key];
                VertexPositions[key] = value - delta;
            }
        }

        if (shouldUpdatePositions)
        {
            foreach (var p in VertexPositions)
            {
                var pos = p.Key.GetPosition();
                pos.center = p.Value;
                p.Key.SetPosition(pos);
            }
        }
    }
}
