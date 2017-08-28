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
        public ComponentDataArray<CrowdAgent> agents;
        [ReadOnly]
        public AgentPaths.AllReadOnly paths;
        [ReadOnly]
        public NativeArray<uint> pathRequestIdForAgent;

        public NativeArray<bool> planPathForAgent;

        public void Execute(int index)
        {
            var agent = agents[index];
            var crowdId = agent.crowdId;
            if (planPathForAgent[crowdId] || index >= agents.Length)
                return;

            if (pathRequestIdForAgent[crowdId] == PathQueryQueueEcs.RequestEcs.invalidId)
            {
                // If there's no path - or close to destination: pick a new destination
                var pathInfo = paths.GetPathInfo(crowdId);
                planPathForAgent[crowdId] = pathInfo.size == 0 || agent.location.valid && math.distance(pathInfo.end.position, agent.location.position) < 0.05f;
            }
        }
    }

    public struct MakePathRequestsJob : IJob
    {
        [ReadOnly]
        public ComponentDataArray<CrowdAgent> agents;

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
                var agent = agents[index];
                var crowdId = agent.crowdId;
                if (planPathForAgent[crowdId])
                {
                    if (!agent.active)
                    {
                        planPathForAgent[crowdId] = false;
                        continue;
                    }

                    if (!agent.location.valid)
                        continue;

                    if (uniqueIdStore[0] == PathQueryQueueEcs.RequestEcs.invalidId)
                    {
                        uniqueIdStore[0] = 1 + PathQueryQueueEcs.RequestEcs.invalidId;
                    }

                    // TODO: move this to a CreateTargetsSystem [#adriant]
                    //var agPos = agent.location.position;
                    //var agVel = agent.velocity;
                    //var randomAngle = ((agPos.x + agPos.y + agPos.z + agVel.x + agVel.y + agVel.z) * (1 + firstAgent + reqIndex) % 2f) * 360f;
                    //var heading = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward;
                    //var dist = Mathf.Abs(randomAngle) % 10f;
                    //var dest = agent.location.position + dist * heading;
                    pathRequests[reqIndex++] = new PathQueryQueueEcs.RequestEcs()
                    {
                        agentIndex = index,
                        agentType = agent.type,
                        mask = NavMesh.AllAreas,
                        uid = uniqueIdStore[0],
                        start = agent.location.position,
                        end = agent.destination
                    };
                    pathRequestIdForAgent[crowdId] = uniqueIdStore[0];
                    uniqueIdStore[0]++;
                    planPathForAgent[crowdId] = false;
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

        public AgentPaths.RangesWritable paths;

        public void Execute(int index)
        {
            if (index >= agents.Length)
                return;

            var agent = agents[index];
            if (!agent.active)
                return;

            var crowdId = agent.crowdId;
            var path = paths.GetPath(crowdId);
            var pathInfo = paths.GetPathInfo(crowdId);

            var i = 0;
            for (; i < pathInfo.size; ++i)
            {
                if (path[i].polygon == agent.location.polygon)
                    break;
            }
            if (i == 0)
                return;

            // Shorten the path
            paths.DiscardFirstNodes(crowdId, i);
        }
    }

    public struct UpdateVelocityJob : IJobParallelFor
    {
        [ReadOnly]
        public AgentPaths.AllReadOnly paths;

        public ComponentDataArray<CrowdAgent> agents;

        public void Execute(int index)
        {
            if (index >= agents.Length)
                return;

            var agent = agents[index];
            if (!agent.active || !agent.location.valid)
                return;

            var crowdId = agents[index].crowdId;
            var pathInfo = paths.GetPathInfo(crowdId);
            if (pathInfo.size > 0)
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
                        steeringTarget = straightPath[1].position;
                    }

                    straightPath.Dispose();
                    straightPathFlags.Dispose();
                }

                var velocity = steeringTarget - currentPos;
                velocity.y = 0.0f;
                agent.velocity = math.any(velocity) ? math.normalize(velocity) : new float3(0);
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
            if (!agent.active)
                return;

            var wantedPos = agent.worldPosition + agent.velocity * dt;

            if (agent.location.valid)
            {
                agent.location = NavMeshQuery.MoveLocation(agent.location, wantedPos);
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
