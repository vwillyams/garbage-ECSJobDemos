using System;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;

public struct AgentPaths
{
    public struct Path
    {
        public int begin;
        public int size;
        public NavMeshLocation start;
        public NavMeshLocation end;
    }

    public struct RangesWritable
    {
        [ReadOnly]
        public NativeArray<PolygonID> nodes;
        public NativeArray<Path> ranges;

        public NativeSlice<PolygonID> GetPath(int index)
        {
            return new NativeSlice<PolygonID>(nodes, ranges[index].begin, ranges[index].size);
        }

        public Path GetPathInfo(int index)
        {
            return ranges[index];
        }

        public void DiscardFirstNodes(int index, int howMany)
        {
            if (howMany == 0)
                return;

            var pathInfo = ranges[index];
            var end = pathInfo.begin + ranges[index].size;
            pathInfo.begin = Math.Min(ranges[index].begin + howMany, end);
            pathInfo.size = end - pathInfo.begin;
            Debug.Assert(pathInfo.size >= 0, "This should not happen");
            ranges[index] = pathInfo;
        }
    }

    public struct AllWritable
    {
        [ReadOnly]
        public int maxPathSize;
        public NativeArray<PolygonID> nodes;
        public NativeArray<Path> ranges;

        public NativeSlice<PolygonID> GetMaxPath(int index)
        {
            return new NativeSlice<PolygonID>(nodes, maxPathSize * index, maxPathSize);
        }

        public void SetPath(int index, NativeSlice<PolygonID> newPath, NavMeshLocation start, NavMeshLocation end)
        {
            var nodeCount = Math.Min(newPath.Length, maxPathSize);
            ranges[index] = new Path
            {
                begin = maxPathSize * index,
                size = nodeCount,
                start = start,
                end = end
            };
            var agentPath = GetMaxPath(index);
            for (var k = 0; k < nodeCount; k++)
            {
                var node = newPath[k];
                agentPath[k] = node;
            }
        }
    }

    public struct AllReadOnly
    {
        [ReadOnly]
        public int maxPathSize;
        [ReadOnly]
        public NativeArray<PolygonID> nodes;
        [ReadOnly]
        public NativeArray<Path> ranges;

        public NativeSlice<PolygonID> GetPath(int index)
        {
            return new NativeSlice<PolygonID>(nodes, ranges[index].begin, ranges[index].size);
        }

        public Path GetPathInfo(int index)
        {
            return ranges[index];
        }
    }

    NativeList<PolygonID> m_PathNodes;
    NativeList<Path> m_PathRanges;
    int m_MaxPathSize;

    public int maxPathSize
    {
        get { return m_MaxPathSize; }
    }

    public int Count
    {
        get { return m_PathRanges.Length; }
    }

    public AgentPaths(int capacity, int maxSize = 64)
    {
        m_MaxPathSize = maxSize;
        m_PathNodes = new NativeList<PolygonID>(capacity * m_MaxPathSize, Allocator.Persistent);
        m_PathRanges = new NativeList<Path>(capacity, Allocator.Persistent);
    }

    public void Dispose()
    {
        m_PathNodes.Dispose();
        m_PathRanges.Dispose();
    }

    public void InitializePath(int index, int nodeCount)
    {
        m_PathRanges[index] = new Path() { begin = m_MaxPathSize * index, size = nodeCount };
    }

    public void AddAgent()
    {
        m_PathRanges.Add(new Path());
        m_PathNodes.ResizeUninitialized(m_MaxPathSize * m_PathRanges.Length);
        InitializePath(m_PathRanges.Length - 1, 0);
    }

    public void AddAgents(int n)
    {
        var world = NavMeshWorld.GetDefaultWorld();
        if (!world.IsValid())
            return;

        var oldLength = m_PathRanges.Length;
        m_PathRanges.ResizeUninitialized(oldLength + n);
        for (var i = oldLength; i < m_PathRanges.Length; i++)
        {
            InitializePath(i, 0);
        }
        m_PathNodes.ResizeUninitialized(m_MaxPathSize * m_PathRanges.Length);
    }

    public Path GetPathInfo(int index)
    {
        return m_PathRanges[index];
    }

    public RangesWritable GetRangesData()
    {
        return new RangesWritable() { nodes = m_PathNodes, ranges = m_PathRanges };
    }

    public AllWritable GetAllData()
    {
        return new AllWritable() { nodes = m_PathNodes, ranges = m_PathRanges, maxPathSize = m_MaxPathSize };
    }

    public AllReadOnly GetReadOnlyData()
    {
        return new AllReadOnly() { nodes = m_PathNodes, ranges = m_PathRanges, maxPathSize = m_MaxPathSize };
    }
}
