using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ECS;
using UnityEngine.Experimental.AI;

public partial class CrowdSystem
{
    public struct CheckPathNeededJob : IJobParallelFor
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
        [ReadOnly]
        public NativeArray<uint> pathRequestIdForAgent;

        public NativeArray<bool1> planPathForAgent;

        public void Execute(int index)
        {
            var agentNavigator = agentNavigators[index];
            if (planPathForAgent[index] || index >= agentNavigators.Length)
                return;

            if (pathRequestIdForAgent[index] == PathQueryQueueEcs.RequestEcs.invalidId)
            {
                planPathForAgent[index] = agentNavigator.newDestinationRequested;
            }
        }
    }

    public struct MakePathRequestsJob : IJob
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;

        public NativeArray<bool1> planPathForAgent;
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
            var reqEnd = pathRequestsRange[k_Start] + pathRequestsRange[k_Count];
            var reqMax = pathRequests.Length - 1;
            var firstAgent = currentAgentIndex[0];
            for (var i = 0; i < agents.Length; ++i)
            {
                if (reqEnd > reqMax)
                    break;

                var index = (i + firstAgent) % agents.Length;
                var agentNavigator = agentNavigators[index];
                if ((planPathForAgent.Length > 0 && planPathForAgent[index])
                    || (agentNavigator.newDestinationRequested && pathRequestIdForAgent[index] == PathQueryQueueEcs.RequestEcs.invalidId))
                {
                    if (!agentNavigator.active)
                    {
                        if (planPathForAgent.Length > 0)
                        {
                            planPathForAgent[index] = false;
                        }
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

                    pathRequests[reqEnd++] = new PathQueryQueueEcs.RequestEcs()
                    {
                        agentIndex = index,
                        agentType = agent.type,
                        mask = NavMesh.AllAreas,
                        uid = uniqueIdStore[0],
                        start = agent.location.position,
                        end = agentNavigator.requestedDestination
                    };
                    pathRequestIdForAgent[index] = uniqueIdStore[0];
                    uniqueIdStore[0]++;
                    if (planPathForAgent.Length > 0)
                    {
                        planPathForAgent[index] = false;
                    }
                    agentNavigator.newDestinationRequested = false;
                    agentNavigators[index] = agentNavigator;
                }
                currentAgentIndex[0] = index;
            }
            pathRequestsRange[k_Count] = reqEnd - pathRequestsRange[k_Start];
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
            if (slotsRemaining <= 0)
                return;

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
            var dst = 0;
            var src = pathRequestsRange[k_Start];
            if (src > dst)
            {
                var count = pathRequestsRange[k_Count];
                var rangeEnd = Math.Min(src + count, pathRequests.Length);
                for (; src < rangeEnd; src++, dst++)
                {
                    pathRequests[dst] = pathRequests[src];
                }
                pathRequestsRange[k_Count] = rangeEnd - pathRequestsRange[k_Start];
                pathRequestsRange[k_Start] = 0;

                // invalidate the remaining requests
                for (; dst < rangeEnd; dst++)
                {
                    var request = pathRequests[dst];
                    request.uid = PathQueryQueueEcs.RequestEcs.invalidId;
                    pathRequests[dst] = request;
                }
            }
        }
    }

    public struct AdvancePathJob : IJobParallelFor
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
        public FixedArrayArray<PolygonID> paths;

        public void Execute(int index)
        {
            var agentNavigator = agentNavigators[index];
            if (!agentNavigator.active)
                return;

            var path = paths[index];
            var agLoc = agents[index].location;
            var i = 0;
            for (; i < agentNavigator.pathSize; ++i)
            {
                if (path[i].polygon == agLoc.polygon)
                    break;
            }

            var agentNotOnPath = i == agentNavigator.pathSize && i > 0;
            if (agentNotOnPath)
            {
                agentNavigator.MoveTo(agentNavigator.requestedDestination);
                agentNavigators[index] = agentNavigator;
            }
            else if (agentNavigator.destinationInView)
            {
                var distToDest = math.distance(agLoc.position, agentNavigator.pathEnd.position );
                var stoppingDistance = 0.1f;
                agentNavigator.destinationReached = distToDest < stoppingDistance;
                agentNavigator.distanceToDestination = distToDest;
                agentNavigator.goToDestination &= !agentNavigator.destinationReached;
                agentNavigators[index] = agentNavigator;
                if (agentNavigator.destinationReached)
                {
                    i = agentNavigator.pathSize;
                }
            }
            if (i == 0 && !agentNavigator.destinationReached)
                return;

//#if DEBUG_CROWDSYSTEM_ASSERTS
            //var discardsPathWhenDestinationNotReached = (i == pathInfo.size) && !agentNavigator.destinationReached;
            //Debug.Assert(!discardsPathWhenDestinationNotReached);
//#endif

            // Shorten the path by discarding the first nodes
            if (i > 0)
            {
                for (int src = i, dst = 0; src < agentNavigator.pathSize; src++, dst++)
                {
                    path[dst] = path[src];
                }
                agentNavigator.pathSize -= i;
                agentNavigators[index] = agentNavigator;
            }
        }
    }

    public struct UpdateVelocityJob : IJobParallelFor
    {
        [ReadOnly]
        public FixedArrayArray<PolygonID> paths;

        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;
        public ComponentDataArray<CrowdAgent> agents;

        [DeallocateOnJobCompletion]
        [NativeDisableParallelForRestriction]
        public NativeArray<NavMeshLocation> straightPath;

        [DeallocateOnJobCompletion]
        [NativeDisableParallelForRestriction]
        public NativeArray<NavMeshStraightPathFlags> straightPathFlags;

        [DeallocateOnJobCompletion]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> vertexSide;

        public void Execute(int index)
        {
            var agent = agents[index];
            var agentNavigator = agentNavigators[index];
            if (!agentNavigator.active || !agent.location.valid)
            {
                if (math.any(agent.velocity))
                {
                    agent.velocity = new float3(0);
                    agents[index] = agent;
                }
                return;
            }

            if (agentNavigator.pathSize > 0 && agentNavigator.goToDestination)
            {
                float3 currentPos = agent.location.position;
                float3 endPos = agentNavigator.pathEnd.position;
                agentNavigator.steeringTarget = endPos;

                if (agentNavigator.pathSize > 1)
                {
                    var cornerCount = 0;
                    var path = paths[index];
                    var pathStatus = PathUtils.FindStraightPath(currentPos, endPos, path, agentNavigator.pathSize, ref straightPath, ref straightPathFlags, ref vertexSide, ref cornerCount, straightPath.Length);

                    if (pathStatus.IsSuccess() && cornerCount > 1)
                    {
                        agentNavigator.steeringTarget = straightPath[1].position;
                        agentNavigator.destinationInView = straightPath[1].polygon == agentNavigator.pathEnd.polygon;
                        agentNavigator.nextCornerSide = vertexSide[1];
                    }
                }
                else
                {
                    agentNavigator.destinationInView = true;
                }
                agentNavigators[index] = agentNavigator;

                var velocity = agentNavigator.steeringTarget - currentPos;
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
            queryQueue.UpdateTimesliced(maxIterations);
        }
    }

    public struct ApplyQueryResultsJob : IJob
    {
        public PathQueryQueueEcs queryQueue;
        public FixedArrayArray<PolygonID> paths;
        public ComponentDataArray<CrowdAgentNavigator> agentNavigators;

        public void Execute()
        {
            if (queryQueue.GetResultPathsCount() > 0)
            {
                queryQueue.CopyResultsTo(ref paths, ref agentNavigators);
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
