using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;
using UnityEngine.ECS;
using UnityEngine.Jobs;

public class PathQueryQueueEcs
{
    NavMeshPathQuery m_Query;
    Queue<RequestEcs> m_Requests;
    NativeList<int> m_AgentIndices;
    NativeList<PolygonID> m_ResultNodes;
    NativeList<AgentPaths.Path> m_ResultRanges;
    AgentPaths.Path m_CurrentInfo;
    int m_CurrentAgentIdx;
    NativeArray<float> m_Costs;

    struct RequestEcs
    {
        public Vector3 start;
        public Vector3 end;
        public int agentIdx;
        public int mask;
    }

    public PathQueryQueueEcs(int nodePoolSize = 2000)
    {
        var world = NavMeshWorld.GetDefaultWorld();
        m_Query = new NavMeshPathQuery(world, nodePoolSize, Allocator.Persistent);
        m_Requests = new Queue<RequestEcs>();
        m_ResultNodes = new NativeList<PolygonID>(nodePoolSize, Allocator.Persistent);
        m_ResultRanges = new NativeList<AgentPaths.Path>(Allocator.Persistent);
        m_AgentIndices = new NativeList<int>(Allocator.Persistent);
        m_Costs = new NativeArray<float>(32, Allocator.Persistent);
        for (var i = 0; i < m_Costs.Length; ++i)
            m_Costs[i] = 1.0f;

        m_CurrentInfo = new AgentPaths.Path();
        m_CurrentAgentIdx = -1;
    }

    public void Dispose()
    {
        m_Costs.Dispose();

        m_ResultNodes.Dispose();
        m_ResultRanges.Dispose();
        m_AgentIndices.Dispose();
    }

    public void QueueRequest(int index, Vector3 start, Vector3 end, int areaMask)
    {
        // TODO: check existing requests for collisions
        var req = new RequestEcs { agentIdx = index, start = start, end = end, mask = areaMask };
        m_Requests.Enqueue(req);
    }

    public bool HasRequestForAgent(int index)
    {
        return m_Requests.Any(req => req.agentIdx == index);
    }

    public void GetResults(ref AgentPaths agentPaths)
    {
        Debug.Assert(m_ResultRanges.Length == m_AgentIndices.Length);
        for (var i = 0; i < m_ResultRanges.Length; i++)
        {
            var index = m_AgentIndices[i];
            var resultPathInfo = m_ResultRanges[i];
            var resultNodes = new NativeSlice<PolygonID>(m_ResultNodes, resultPathInfo.begin, resultPathInfo.size);
            agentPaths.SetPath(index, resultNodes, resultPathInfo.start, resultPathInfo.end);
        }
    }

    public void ClearResults()
    {
        m_ResultNodes.Clear();
        m_ResultRanges.Clear();
        m_AgentIndices.Clear();
    }

    public void UpdateTimeliced(int maxIter = 100)
    {
        while (maxIter > 0)
        {
            if (m_CurrentAgentIdx < 0)
            {
                if (m_Requests.Count == 0)
                    return;

                // Initialize a new request
                var req = m_Requests.Dequeue();
                m_CurrentInfo.begin = 0;
                m_CurrentInfo.size = 0;
                m_CurrentInfo.start = NavMeshQuery.MapLocation(req.start, 10.0f * Vector3.one, 0, req.mask);
                m_CurrentInfo.end = NavMeshQuery.MapLocation(req.end, 10.0f * Vector3.one, 0, req.mask);
                if (!m_CurrentInfo.start.valid || !m_CurrentInfo.end.valid)
                    continue;

                var status = m_Query.InitSlicedFindPath(m_CurrentInfo.start, m_CurrentInfo.end, 0, m_Costs, req.mask);
                if (status != PathQueryStatus.Failure)
                {
                    m_CurrentAgentIdx = req.agentIdx;
                }
            }

            if (m_CurrentAgentIdx >= 0)
            {
                // Continue existing request
                var niter = 0;
                var status = m_Query.UpdateSlicedFindPath(maxIter, out niter);
                maxIter -= niter;

                if (status == PathQueryStatus.Success)
                {
                    var npath = 0;
                    status = m_Query.FinalizeSlicedFindPath(out npath);
                    if (status == PathQueryStatus.Success)
                    {
                        var resPolygons = new NativeArray<PolygonID>(npath, Allocator.TempJob);
                        m_CurrentInfo.size = m_Query.GetPathResult(resPolygons);
                        if (m_CurrentInfo.size > 0)
                        {
                            m_CurrentInfo.begin = m_ResultNodes.Length;
                            for (var i = 0; i < npath; i++)
                            {
                                m_ResultNodes.Add(resPolygons[i]);
                            }
                            m_ResultRanges.Add(m_CurrentInfo);
                            m_AgentIndices.Add(m_CurrentAgentIdx);
                        }
                        resPolygons.Dispose();
                    }

                    m_CurrentAgentIdx = -1;
                }
            }
        }
    }
}
