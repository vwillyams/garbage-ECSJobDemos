using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;
using UnityEngine.ECS;
using UnityEngine.Jobs;

public struct PathQueryQueueEcs
{
    struct RequestEcs
    {
        public Vector3 start;
        public Vector3 end;
        public int agentIdx;
        public int mask;
    }

    struct QueryQueueState
    {
        public int requestCount;
        public int requestIndex;
        public int resultNodesCount;
        public int resultPathsCount;
        public int currentAgentIndex;
        public AgentPaths.Path currentPathRequest;
    }

    NavMeshPathQuery m_Query;
    NativeArray<RequestEcs> m_Requests;
    NativeArray<PolygonID> m_ResultNodes;
    NativeArray<AgentPaths.Path> m_ResultRanges;
    NativeArray<int> m_AgentIndices;
    NativeArray<float> m_Costs;
    NativeArray<QueryQueueState> m_State;

    public PathQueryQueueEcs(int nodePoolSize)
    {
        var world = NavMeshWorld.GetDefaultWorld();
        m_Query = new NavMeshPathQuery(world, nodePoolSize, Allocator.Persistent);
        m_Requests = new NativeArray<RequestEcs>(100, Allocator.Persistent);
        m_ResultNodes = new NativeArray<PolygonID>(2 * nodePoolSize, Allocator.Persistent);
        m_ResultRanges = new NativeArray<AgentPaths.Path>(100, Allocator.Persistent);
        m_AgentIndices = new NativeArray<int>(100, Allocator.Persistent);
        m_Costs = new NativeArray<float>(32, Allocator.Persistent);
        for (var i = 0; i < m_Costs.Length; ++i)
            m_Costs[i] = 1.0f;

        m_State = new NativeArray<QueryQueueState>(1, Allocator.Persistent);
        m_State[0] = new QueryQueueState()
        {
            requestCount = 0,
            requestIndex = 0,
            resultNodesCount = 0,
            resultPathsCount = 0,
            currentAgentIndex = -1,
            currentPathRequest = new AgentPaths.Path()
        };
    }

    public void Dispose()
    {
        m_Requests.Dispose();
        m_ResultNodes.Dispose();
        m_ResultRanges.Dispose();
        m_AgentIndices.Dispose();
        m_Costs.Dispose();
        m_State.Dispose();
    }

    public bool QueueRequest(int index, Vector3 start, Vector3 end, int areaMask)
    {
        var state = m_State[0];
        if (state.requestCount == m_Requests.Length)
            return false;

        // TODO: check existing requests for collisions
        m_Requests[state.requestCount] = new RequestEcs { agentIdx = index, start = start, end = end, mask = areaMask };
        state.requestCount++;
        m_State[0] = state;

        return true;
    }

    public int GetRequestCount()
    {
        return m_State[0].requestCount;
    }

    public bool IsEmpty()
    {
        var state = m_State[0];
        return state.requestCount == 0 && state.currentAgentIndex < 0;
    }

    public bool HasRequestForAgent(int index)
    {
        var state = m_State[0];
        if (state.currentAgentIndex == index)
            return true;

        for (var i = 0; i < state.requestCount; i++)
        {
            if (m_Requests[i].agentIdx == index)
                return true;
        }

        return false;
    }

    public int GetResultPathsCount()
    {
        return m_State[0].resultPathsCount;
    }

    public void CopyResultsTo(ref AgentPaths.AllWritable agentPaths)
    {
        var state = m_State[0];
        for (var i = 0; i < state.resultPathsCount; i++)
        {
            var index = m_AgentIndices[i];
            var resultPathInfo = m_ResultRanges[i];
            var resultNodes = new NativeSlice<PolygonID>(m_ResultNodes, resultPathInfo.begin, resultPathInfo.size);
            agentPaths.SetPath(index, resultNodes, resultPathInfo.start, resultPathInfo.end);
        }
    }

    public void ClearResults()
    {
        var state = m_State[0];
        state.resultNodesCount = 0;
        state.resultPathsCount = 0;
        m_State[0] = state;
    }

    public void UpdateTimeliced(int maxIter = 100)
    {
        var state = m_State[0];
        while (maxIter > 0 && (state.currentAgentIndex >= 0 || state.requestCount > 0 && state.requestIndex < state.requestCount))
        {
            if (state.currentAgentIndex < 0 && state.requestCount > 0 && state.requestIndex < state.requestCount)
            {
                // Initialize a new request
                var req = m_Requests[state.requestIndex];
                state.requestIndex++;
                var startLoc = NavMeshQuery.MapLocation(req.start, 10.0f * Vector3.one, 0, req.mask);
                var endLoc = NavMeshQuery.MapLocation(req.end, 10.0f * Vector3.one, 0, req.mask);
                if (!startLoc.valid || !endLoc.valid)
                    continue;

                state.currentPathRequest = new AgentPaths.Path()
                {
                    begin = 0,
                    size = 0,
                    start = startLoc,
                    end = endLoc
                };

                var status = m_Query.InitSlicedFindPath(startLoc, endLoc, 0, m_Costs, req.mask);
                if (status != PathQueryStatus.Failure)
                {
                    state.currentAgentIndex = req.agentIdx;
                }
            }

            if (state.currentAgentIndex >= 0)
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
                        // TODO: Maybe add a method to get beforehand the number of result nodes and check if it will fit in the remaining space in m_ResultNodes [#aturcanu]

                        var resPolygons = new NativeArray<PolygonID>(npath, Allocator.TempJob);
                        var pathInfo = state.currentPathRequest;
                        pathInfo.size = m_Query.GetPathResult(resPolygons);
                        if (pathInfo.size > 0)
                        {
                            Debug.Assert(pathInfo.size + state.resultNodesCount <= m_ResultNodes.Length);

                            pathInfo.begin = state.resultNodesCount;
                            for (var i = 0; i < npath; i++)
                            {
                                m_ResultNodes[state.resultNodesCount] = resPolygons[i];
                                state.resultNodesCount++;
                            }
                            m_ResultRanges[state.resultPathsCount] = pathInfo;
                            m_AgentIndices[state.resultPathsCount] = state.currentAgentIndex;
                            state.resultPathsCount++;
                        }
                        state.currentPathRequest = pathInfo;
                        resPolygons.Dispose();
                    }

                    state.currentAgentIndex = -1;
                }
            }
        }

        // discard processed requests
        if (state.requestIndex > 0)
        {
            var iDest = 0;
            var iSrc = state.requestIndex;
            for (; iSrc < state.requestCount; iSrc++, iDest++)
            {
                m_Requests[iDest] = m_Requests[iSrc];
            }
            state.requestCount -= state.requestIndex;
            state.requestIndex = 0;
        }

        m_State[0] = state;
    }
}
