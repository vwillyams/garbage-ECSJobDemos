using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Collections;
using UnityEngine.ECS;
using UnityEngine.Experimental.AI;
using UnityEngine.Jobs;

public partial class CrowdSystem
{
    public struct CheckPathNeededJob : IJobParallelFor
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
        [ReadOnly]
        public AgentPaths.AllReadOnly paths;
        [ReadOnly]
        public NativeArray<uint> pathRequestIdForAgent;

        public NativeArray<bool> planPathForAgent;

        public void Execute(int index)
        {
            var agentNavigator = agentNavigators[index];
            var crowdId = agentNavigator.crowdId;
            if (planPathForAgent[crowdId] || index >= agentNavigators.Length)
                return;

            if (pathRequestIdForAgent[crowdId] == PathQueryQueueEcs.RequestEcs.invalidId)
            {
                planPathForAgent[crowdId] = agentNavigator.newDestinationRequested;
            }
        }
    }

    public struct MakePathRequestsJob : IJob
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;

        public NativeArray<bool> planPathForAgent;
        public NativeArray<uint> pathRequestIdForAgent;
        public NativeArray<PathQueryQueueEcs.RequestEcs> pathRequests;
        public NativeArray<int> pathRequestsRange;
        public NativeArray<uint> uniqueIdStore;
        public NativeArray<int> currentAgentIndex;

        public void Execute()
        {
            if (agents.Length == 0)
                return;

            // add new requests to the end of the range
            var reqIndex = pathRequestsRange[k_Start] + pathRequestsRange[k_Count];
            var reqMax = pathRequests.Length - 1;
            var firstAgent = currentAgentIndex[0];
            for (uint i = 0; i < agents.Length; ++i)
            {
                if (reqIndex > reqMax)
                    break;

                var index = (int)(i + firstAgent) % agents.Length;
                var agentNavigator = agentNavigators[index];
                var crowdId = agentNavigator.crowdId;
                if (planPathForAgent[crowdId])
                {
                    if (!agentNavigator.active)
                    {
                        planPathForAgent[crowdId] = false;
                        agentNavigator.newDestinationRequested = false;
                        agentNavigators[index] = agentNavigator;
                        continue;
                    }

                    var agent = agents[index];
                    if (!agent.location.valid)
                        continue;

                    if (uniqueIdStore[0] == PathQueryQueueEcs.RequestEcs.invalidId)
                    {
                        uniqueIdStore[0] = 1 + PathQueryQueueEcs.RequestEcs.invalidId;
                    }

                    pathRequests[reqIndex++] = new PathQueryQueueEcs.RequestEcs()
                    {
                        agentIndex = index,
                        agentType = agent.type,
                        mask = NavMesh.AllAreas,
                        uid = uniqueIdStore[0],
                        start = agent.location.position,
                        end = agentNavigator.requestedDestination
                    };
                    pathRequestIdForAgent[crowdId] = uniqueIdStore[0];
                    uniqueIdStore[0]++;
                    planPathForAgent[crowdId] = false;
                    agentNavigator.newDestinationRequested = false;
                    agentNavigators[index] = agentNavigator;
                }
                currentAgentIndex[0] = index;
            }
            pathRequestsRange[k_Count] = reqIndex - pathRequestsRange[k_Start];
        }
    }

    public struct EnqueueRequestsInQueriesJob : IJob
    {
        public NativeArray<PathQueryQueueEcs.RequestEcs> pathRequests;
        public NativeArray<int> pathRequestsRange;
        public PathQueryQueueEcs queryQueue;
        public int maxRequestsInQueue;

        public void Execute()
        {
            var reqCount = pathRequestsRange[k_Count];
            if (reqCount == 0)
                return;

            var reqIdx = pathRequestsRange[k_Start];
            var slotsRemaining = maxRequestsInQueue - queryQueue.GetRequestCount();
            var rangeEnd = reqIdx + Math.Min(slotsRemaining, reqCount);
            for (; reqIdx < rangeEnd; reqIdx++)
            {
                var pathRequest = pathRequests[reqIdx];
                if (queryQueue.Enqueue(pathRequest))
                {
                    pathRequest.uid = PathQueryQueueEcs.RequestEcs.invalidId;
                    pathRequests[reqIdx] = pathRequest;
                }
                else
                {
                    reqIdx++;
                    break;
                }
            }

            pathRequestsRange[k_Count] = reqCount - (reqIdx - pathRequestsRange[k_Start]);
            pathRequestsRange[k_Start] = reqIdx;
        }
    }

    public struct ForgetMovedRequestsJob : IJob
    {
        public NativeArray<PathQueryQueueEcs.RequestEcs> pathRequests;
        public NativeArray<int> pathRequestsRange;

        public void Execute()
        {
            var dest = 0;
            var src = pathRequestsRange[k_Start];
            if (src > dest)
            {
                var count = pathRequestsRange[k_Count];
                var rangeEnd = Math.Min(src + count, pathRequests.Length);
                for (; src < rangeEnd; src++, dest++)
                {
                    pathRequests[dest] = pathRequests[src];
                }
                pathRequestsRange[k_Count] = rangeEnd - pathRequestsRange[k_Start];
                pathRequestsRange[k_Start] = 0;

                // invalidate the remaining requests
                for (; dest < rangeEnd; dest++)
                {
                    var request = pathRequests[dest];
                    request.uid = PathQueryQueueEcs.RequestEcs.invalidId;
                    pathRequests[dest] = request;
                }
            }
        }
    }

    public struct AdvancePathJob : IJobParallelFor
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
        public AgentPaths.RangesWritable paths;

        public void Execute(int index)
        {
            if (index >= agentNavigators.Length)
                return;

            var agentNavigator = agentNavigators[index];
            if (!agentNavigator.active)
                return;

            var crowdId = agentNavigator.crowdId;
            var path = paths.GetPath(crowdId);
            var pathInfo = paths.GetPathInfo(crowdId);

            var agLoc = agents[index].location;
            var i = 0;
            for (; i < pathInfo.size; ++i)
            {
                if (path[i].polygon == agLoc.polygon)
                    break;
            }

            var agentNotOnPath = i == pathInfo.size;
            if (agentNotOnPath)
            {
                agentNavigator.goToDestination = false;
                agentNavigator.destinationReached = true;
                agentNavigators[index] = agentNavigator;
            }
            else if (agentNavigator.destinationInView)
            {
                var distToDest = math.distance(agLoc.position, pathInfo.end.position);
                var stoppingDistance = 0.1f;
                agentNavigator.destinationReached = distToDest < stoppingDistance;
                agentNavigator.distanceToDestination = distToDest;
                agentNavigator.goToDestination &= !agentNavigator.destinationReached;
                agentNavigators[index] = agentNavigator;
                if (agentNavigator.destinationReached)
                {
                    i = pathInfo.size;
                }
            }
            if (i == 0 && !agentNavigator.destinationReached)
                return;

            //if ((i == pathInfo.size) && !agentNavigator.destinationReached)
            //{
            //    Debug.LogWarning("Agent " + index + " discards path when destination not reached");
            //}

            // Shorten the path
            paths.DiscardFirstNodes(crowdId, i);
        }
    }

    public struct UpdateVelocityJob : IJobParallelFor
    {
        [ReadOnly]
        public AgentPaths.AllReadOnly paths;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
        public ComponentDataArray<CrowdAgent> agents;

        public void Execute(int index)
        {
            if (index >= agents.Length)
                return;

            var agent = agents[index];
            var agentNavigator = agentNavigators[index];
            if (!agentNavigator.active || !agent.location.valid)
                return;

            var crowdId = agentNavigator.crowdId;
            var pathInfo = paths.GetPathInfo(crowdId);
            if (pathInfo.size > 0 && agentNavigator.goToDestination)
            {
                float3 currentPos = agent.location.position;
                float3 endPos = pathInfo.end.position;
                var steeringTarget = endPos;

                if (pathInfo.size > 1)
                {
                    const int maxCorners = 2;
                    var cornerCount = 0;
                    var straightPath = new NativeArray<NavMeshLocation>(maxCorners, Allocator.TempJob);
                    var straightPathFlags = new NativeArray<NavMeshStraightPathFlags>(straightPath.Length, Allocator.TempJob);
                    var pathStatus = PathUtils.FindStraightPath(currentPos, endPos, paths.GetPath(crowdId), pathInfo.size, ref straightPath, ref straightPathFlags, ref cornerCount, straightPath.Length);
                    if (pathStatus == PathQueryStatus.Success && cornerCount > 1)
                    {
                        var nextCornerLoc = straightPath[1];
                        steeringTarget = nextCornerLoc.position;
                        agentNavigator.destinationInView = nextCornerLoc.polygon == pathInfo.end.polygon;
                    }

                    straightPath.Dispose();
                    straightPathFlags.Dispose();
                }
                else
                {
                    agentNavigator.destinationInView = true;
                }
                agentNavigators[index] = agentNavigator;

                var velocity = steeringTarget - currentPos;
                velocity.y = 0.0f;
                agent.velocity = math.any(velocity) ? agentNavigator.speed * math.normalize(velocity) : new float3(0);
            }
            else
            {
                agent.velocity = new float3(0);
            }

            // TODO: add avoidance as a job after this one

            agents[index] = agent;
        }
    }

    public struct MoveLocationsJob : IJobParallelFor
    {
        public ComponentDataArray<CrowdAgent> agents;
        public float dt;

        public void Execute(int index)
        {
            if (index >= agents.Length)
                return;

            var agent = agents[index];
            var wantedPos = agent.worldPosition + agent.velocity * dt;

            if (agent.location.valid)
            {
                if (math.any(agent.velocity))
                {
                    agent.location = NavMeshQuery.MoveLocation(agent.location, wantedPos);
                }
            }
            else
            {
                // Constrain the position using the location
                // TODO There are two positions that could be mapped: current agent position or the wanted position [#adriant]
                agent.location = NavMeshQuery.MapLocation(wantedPos, 3 * Vector3.one, 0);
            }
            agent.worldPosition = agent.location.position;

            agents[index] = agent;

            // TODO: Patch the path here and remove AdvancePathJob. The path can get shorter, longer, the same.
            //       For extending paths - it requires a variant of MoveLocation returning the visited paths.
        }
    }

    public struct UpdateQueriesJob : IJob
    {
        public PathQueryQueueEcs queryQueue;
        public int maxIterations;

        public void Execute()
        {
            queryQueue.UpdateTimeliced(maxIterations);
        }
    }

    public struct ApplyQueryResultsJob : IJob
    {
        public PathQueryQueueEcs queryQueue;
        public AgentPaths.AllWritable paths;

        public void Execute()
        {
            if (queryQueue.GetResultPathsCount() > 0)
            {
                queryQueue.CopyResultsTo(ref paths);
                queryQueue.ClearResults();
            }
        }
    }

    public struct QueryCleanupJob : IJob
    {
        public PathQueryQueueEcs queryQueue;
        public NativeArray<uint> pathRequestIdForAgent;

        public void Execute()
        {
            queryQueue.CleanupProcessedRequests(ref pathRequestIdForAgent);
        }
    }
}
