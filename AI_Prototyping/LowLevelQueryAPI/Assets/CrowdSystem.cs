using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;
using UnityEngine.ECS;

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
    public int maxPathSize { get { return m_MaxPathSize; } }
    public int Count { get { return m_PathRanges.Length; } }

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

//[UpdateAfter(typeof(TargetsSystem))]
    //[InjectTuples(1)]
    //ComponentDataArray<AgentTarget> m_Targets;
public class CrowdSystem : JobComponentSystem
{
    public bool drawDebug = false;

    [InjectTuples]
    ComponentDataArray<CrowdAgent> m_Agents;

    NativeList<bool> m_PlanPathForAgent;
    NativeList<uint> m_PathRequestIdForAgent;
    NativeList<PathQueryQueueEcs.RequestEcs> m_PathRequests;
    NativeArray<int> m_PathRequestsRange;
    NativeArray<uint> m_UniqueIdStore;
    NativeArray<int> m_CurrentAgentIndex;

    const int k_MaxQueryNodes = 2000;
    const int k_MaxRequestsPerQuery = 100;
    const int k_QueryCount = 7;
    const int k_PathRequestsPerTick = 24; // how many requests can be added to the query queues per tick

    PathQueryQueueEcs[] m_QueryQueues;
    bool[] m_IsEmptyQueryQueue;
    NativeArray<JobHandle> m_AfterQueriesProcessed;
    NativeArray<UpdateQueriesJob> m_QueryJobs;

    AgentPaths m_AgentPaths;

    const int k_Start = 0;
    const int k_Count = 1;
    const int k_DataSize = 2;

    override protected void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        var world = NavMeshWorld.GetDefaultWorld();
        var queryCount = world.IsValid() ? k_QueryCount : 0;

        var agentCount = world.IsValid() ? capacity : 0;
        m_AgentPaths = new AgentPaths(agentCount, 128);
        m_PlanPathForAgent = new NativeList<bool>(agentCount, Allocator.Persistent);
        m_PathRequestIdForAgent = new NativeList<uint>(agentCount, Allocator.Persistent);
        m_PathRequests = new NativeList<PathQueryQueueEcs.RequestEcs>(k_PathRequestsPerTick, Allocator.Persistent);
        m_PathRequests.ResizeUninitialized(k_PathRequestsPerTick);
        for (var i = 0; i < m_PathRequests.Length; i++)
        {
            m_PathRequests[i] = new PathQueryQueueEcs.RequestEcs { uid = PathQueryQueueEcs.RequestEcs.invalidId };
        }
        m_PathRequestsRange = new NativeArray<int>(k_DataSize, Allocator.Persistent);
        m_PathRequestsRange[k_Start] = 0;
        m_PathRequestsRange[k_Count] = 0;
        m_UniqueIdStore = new NativeArray<uint>(1, Allocator.Persistent);
        m_CurrentAgentIndex = new NativeArray<int>(1, Allocator.Persistent);
        m_CurrentAgentIndex[0] = 0;

        m_QueryQueues = new PathQueryQueueEcs[queryCount];
        m_QueryJobs = new NativeArray<UpdateQueriesJob>(queryCount, Allocator.Persistent);
        m_AfterQueriesProcessed = new NativeArray<JobHandle>(queryCount, Allocator.Persistent);
        m_IsEmptyQueryQueue = new bool[queryCount];
        for (var i = 0; i < m_QueryQueues.Length; i++)
        {
            m_QueryQueues[i] = new PathQueryQueueEcs(k_MaxQueryNodes, k_MaxRequestsPerQuery);
            m_QueryJobs[i] = new UpdateQueriesJob() { maxIterations = 10 + i * 5, queryQueue = m_QueryQueues[i] };
            m_AfterQueriesProcessed[i] = new JobHandle();
            m_IsEmptyQueryQueue[i] = true;
        }
    }

    override protected void OnDestroyManager()
    {
        CompleteDependency();

        base.OnDestroyManager();

        for (var i = 0; i < m_QueryQueues.Length; i++)
        {
            m_QueryQueues[i].Dispose();
        }
        m_AfterQueriesProcessed.Dispose();
        m_QueryJobs.Dispose();
        m_AgentPaths.Dispose();
        m_PlanPathForAgent.Dispose();
        m_PathRequestIdForAgent.Dispose();
        m_PathRequests.Dispose();
        m_PathRequestsRange.Dispose();
        m_UniqueIdStore.Dispose();
        m_CurrentAgentIndex.Dispose();
    }

    public void AddAgents(int n)
    {
        if (n <= 0)
            return;

        var oldLength = m_PlanPathForAgent.Length;
        m_PlanPathForAgent.ResizeUninitialized(oldLength + n);
        m_PathRequestIdForAgent.ResizeUninitialized(m_PlanPathForAgent.Length);
        for (var i = oldLength; i < m_PlanPathForAgent.Length; i++)
        {
            m_PlanPathForAgent[i] = true;
            m_PathRequestIdForAgent[i] = PathQueryQueueEcs.RequestEcs.invalidId;
        }
    }

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
            if (planPathForAgent[index] || index >= agents.Length)
                return;

            if (pathRequestIdForAgent[index] == PathQueryQueueEcs.RequestEcs.invalidId)
            {
                // If there's no path - or close to destination: pick a new destination
                var agent = agents[index];
                var pathInfo = paths.GetPathInfo(index);
                planPathForAgent[index] = pathInfo.size == 0 || agent.location.valid && math.distance(pathInfo.end.position, agent.location.position) < 0.05f;
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
            for (uint i = 0; i < planPathForAgent.Length; ++i)
            {
                if (reqIndex > reqMax)
                    break;

                var index = (int)(i + firstAgent) % planPathForAgent.Length;
                if (planPathForAgent[index])
                {
                    var agent = agents[index];
                    if (!agent.location.valid)
                        continue;

                    if (uniqueIdStore[0] == PathQueryQueueEcs.RequestEcs.invalidId)
                    {
                        uniqueIdStore[0] = 1 + PathQueryQueueEcs.RequestEcs.invalidId;
                    }

                    var agPos = agent.location.position;
                    var agVel = agent.velocity;
                    var randomAngle = ((agPos.x + agPos.y + agPos.z + agVel.x + agVel.y + agVel.z) * (1 + firstAgent + reqIndex) % 2f) * 360f;
                    var heading = Quaternion.Euler(0, randomAngle, 0) * Vector3.forward;
                    var dist = Mathf.Abs(randomAngle) % 10f;
                    var dest = agent.location.position + dist * heading;
                    pathRequests[reqIndex++] = new PathQueryQueueEcs.RequestEcs()
                    {
                        agentIndex = index,
                        agentType = 0,
                        mask = NavMesh.AllAreas,
                        uid = uniqueIdStore[0],
                        start = agent.location.position,
                        end = dest
                    };
                    pathRequestIdForAgent[index] = uniqueIdStore[0];
                    uniqueIdStore[0]++;
                    planPathForAgent[index] = false;
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

            var path = paths.GetPath(index);
            var pathInfo = paths.GetPathInfo(index);

            var i = 0;
            for (; i < pathInfo.size; ++i)
            {
                if (path[i].polygon == agents[index].location.polygon)
                    break;
            }
            if (i == 0)
                return;

            // Shorten the path
            paths.DiscardFirstNodes(index, i);
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
            if (!agent.location.valid)
                return;

            var pathInfo = paths.GetPathInfo(index);
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
                    var pathStatus = PathUtils.FindStraightPath(currentPos, endPos, paths.GetPath(index), pathInfo.size, ref straightPath, ref straightPathFlags, ref cornerCount, straightPath.Length);
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

    void DrawDebug()
    {
        if (!drawDebug)
            return;

        for (var i = 0; i < m_Agents.Length; ++i)
        {
            var agent = m_Agents[i];
            float3 offset = 0.5f * Vector3.up;

            //Debug.DrawRay(agent.worldPosition + offset, agent.velocity, Color.cyan);

            var pathInfo = m_AgentPaths.GetPathInfo(i);
            if (pathInfo.size == 0 || m_PlanPathForAgent[i] || m_PathRequestIdForAgent[i] != PathQueryQueueEcs.RequestEcs.invalidId)
            {
                var requestInProcess = m_PathRequestIdForAgent[i] != PathQueryQueueEcs.RequestEcs.invalidId;
                var stateColor = requestInProcess ? Color.yellow : (m_PlanPathForAgent[i] ? Color.magenta : Color.red);
                Debug.DrawRay(agent.worldPosition + offset, 0.5f * Vector3.up, stateColor);
                continue;
            }

            offset = 0.9f * offset;
            float3 pathEndPos = pathInfo.end.position;
            Debug.DrawLine(agent.worldPosition + offset, pathEndPos, Color.black);
        }
    }

    override protected void OnUpdate()
    {
        base.OnUpdate();

        if (m_Agents.Length == 0)
            return;

        //
        // Prepare data on the main thread
        //
        CompleteDependency();

        var missingPaths = m_Agents.Length - m_AgentPaths.Count;
        if (missingPaths > 0)
        {
            m_AgentPaths.AddAgents(missingPaths);
            AddAgents(missingPaths);
        }
        Debug.Assert(m_Agents.Length <= m_AgentPaths.Count && m_Agents.Length <= m_PathRequestIdForAgent.Length && m_Agents.Length <= m_PlanPathForAgent.Length,
            "" + m_Agents.Length + " agents, " + m_AgentPaths.Count + " path slots, " + m_PathRequestIdForAgent.Length + " path request IDs, " + m_PlanPathForAgent.Length + " slots for WantsPath");

        //{
        //    var rangeEnd = m_PathRequestsRange[k_Start] + m_PathRequestsRange[k_Count];
        //    for (var i = m_PathRequestsRange[k_Start]; i < rangeEnd; i++)
        //    {
        //        Debug.Assert(m_PathRequests[i].uid != PathQueryQueueEcs.RequestEcs.invalidId, "Path request " + i + " should have a valid unique ID");
        //    }
        //}

        DrawDebug();

        var requestsPerQueue = int.MaxValue;
        if (m_QueryQueues.Length > 0)
        {
            var existingRequests = m_QueryQueues.Sum(queue => queue.GetRequestCount());
            var requestCount = existingRequests + m_PathRequestsRange[k_Count];
            requestsPerQueue = requestCount / m_QueryQueues.Length;
            if (requestCount % m_QueryQueues.Length != 0)
                requestsPerQueue += 1;
        }

        for (var i = 0; i < m_QueryQueues.Length; i++)
        {
            m_IsEmptyQueryQueue[i] = m_QueryQueues[i].IsEmpty();
        }


        //
        // Begin scheduling jobs
        //
        var afterQueriesCleanup = new JobHandle();
        for (var i = 0; i < m_QueryQueues.Length; i++)
        {
            var queue = m_QueryQueues[i];
            if (queue.GetProcessedRequestCount() > 0)
            {
                var queryCleanupJob = new QueryCleanupJob()
                {
                    queryQueue = queue,
                    pathRequestIdForAgent = m_PathRequestIdForAgent
                };
                afterQueriesCleanup = queryCleanupJob.Schedule(afterQueriesCleanup);
            }
        }

        var pathNeededJob = new CheckPathNeededJob()
        {
            agents = m_Agents,
            planPathForAgent = m_PlanPathForAgent,
            pathRequestIdForAgent = m_PathRequestIdForAgent,
            paths = m_AgentPaths.GetReadOnlyData()
        };
        var afterPathNeedChecked = pathNeededJob.Schedule(m_Agents.Length, 25, afterQueriesCleanup);

        //TODO: resize the m_PathRequests buffer as needed [#adriant]
        var makeRequestsJob = new MakePathRequestsJob()
        {
            agents = m_Agents,
            planPathForAgent = m_PlanPathForAgent,
            pathRequestIdForAgent = m_PathRequestIdForAgent,
            pathRequests = m_PathRequests,
            pathRequestsRange = m_PathRequestsRange,
            currentAgentIndex = m_CurrentAgentIndex,
            uniqueIdStore = m_UniqueIdStore
        };
        var afterRequestsCreated = makeRequestsJob.Schedule(afterPathNeedChecked);

        var afterRequestsMovedToQueries = afterRequestsCreated;
        if (m_QueryQueues.Length > 0)
        {
            foreach (var queue in m_QueryQueues)
            {
                var enqueuingJob = new EnqueueRequestsInQueriesJob
                {
                    pathRequests = m_PathRequests,
                    pathRequestsRange = m_PathRequestsRange,
                    maxRequestsInQueue = requestsPerQueue,
                    queryQueue = queue
                };
                afterRequestsMovedToQueries = enqueuingJob.Schedule(afterRequestsMovedToQueries);
            }
        }

        var forgetMovedRequestsJob = new ForgetMovedRequestsJob
        {
            pathRequests = m_PathRequests,
            pathRequestsRange = m_PathRequestsRange
        };
        var afterMovedRequestsForgotten = forgetMovedRequestsJob.Schedule(afterRequestsMovedToQueries);

        var queriesScheduled = 0;
        for (var i = 0; i < m_QueryJobs.Length; ++i)
        {
            if (m_IsEmptyQueryQueue[i])
                continue;

            m_AfterQueriesProcessed[i] = m_QueryJobs[i].Schedule(afterMovedRequestsForgotten);
            queriesScheduled++;
        }
        var afterQueriesProcessed = queriesScheduled > 0 ? JobHandle.CombineDependencies(m_AfterQueriesProcessed) : afterMovedRequestsForgotten;

        var afterPathsAdded = afterQueriesProcessed;
        foreach (var queue in m_QueryQueues)
        {
            var resultsJob = new ApplyQueryResultsJob() { queryQueue = queue, paths = m_AgentPaths.GetAllData() };
            afterPathsAdded = resultsJob.Schedule(afterPathsAdded);
        }

        var advance = new AdvancePathJob() { agents = m_Agents, paths = m_AgentPaths.GetRangesData() };
        var afterPathsTrimmed = advance.Schedule(m_Agents.Length, 20, afterPathsAdded);

        var vel = new UpdateVelocityJob() { agents = m_Agents, paths = m_AgentPaths.GetReadOnlyData() };
        var afterVelocitiesUpdated = vel.Schedule(m_Agents.Length, 15, afterPathsTrimmed);

        var move = new MoveLocationsJob() { agents = m_Agents, dt = Time.deltaTime };
        var afterAgentsMoved = move.Schedule(m_Agents.Length, 10, afterVelocitiesUpdated);

        AddDependency(afterAgentsMoved);

        // TODO: job safety for navmesh mutation
        // NavMeshManager.DidScheduleQueryJobs(afterAgentsMoved);
    }
}
