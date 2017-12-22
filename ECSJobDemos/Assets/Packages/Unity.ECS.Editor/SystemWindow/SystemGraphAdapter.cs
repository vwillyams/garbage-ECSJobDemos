using System;
using System.Collections.Generic;
using System.Windows;
using QuickGraph;
using System.Linq;
using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;

namespace UnityEditor.ECS
{
    public class SystemGraphAdapter : BidirectionalGraph<SystemViewData, EdgeAdapter>
    {
        public Dictionary<SystemViewData, Point> vertexPositions { get; set; }
        public Dictionary<SystemViewData, Size> vertexSizes { get; set; }

        public SystemGraphAdapter(SystemGraphState state)
        {
            vertexPositions = new Dictionary<SystemViewData, Point>();
            vertexSizes = new Dictionary<SystemViewData, Size>();

            state.systemViews.ForEach(node =>
            {
                AddVertex(node);

                var position = node.position.position;
                vertexPositions[node] = new Point(position.x, position.y);

                var size = node.position.size;
                vertexSizes[node] = new Size(size.x, size.y);
            });

            state.systemViews.ForEach(node =>
            {
                node.updateAfter.ForEach(otherId =>
                {
                    AddEdge(new EdgeAdapter(Guid.NewGuid().ToString(), node, state.systemViews[otherId]));
                });
                node.updateBefore.ForEach(otherId =>
                {
                    AddEdge(new EdgeAdapter(Guid.NewGuid().ToString(), state.systemViews[otherId], node));
                });
            });
        }
    }

}
