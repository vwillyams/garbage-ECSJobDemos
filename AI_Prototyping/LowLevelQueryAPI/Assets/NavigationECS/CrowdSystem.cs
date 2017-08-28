using System.Linq;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Collections;
using UnityEngine.Experimental.AI;
using UnityEngine.ECS;

//[UpdateAfter(typeof(TargetsSystem))]
//[InjectTuples(1)]
//ComponentDataArray<AgentTarget> m_Targets;
public partial class CrowdSystem : JobComponentSystem
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
            var idx = m_AgentPaths.Count;
            m_AgentPaths.AddAgents(missingPaths);
            AddAgents(missingPaths);
            var endIdx = m_Agents.Length + idx;
            for (var i = idx; i < endIdx; i++)
            {
                var k = i % m_Agents.Length;
                var agent = m_Agents[k];
                if (agent.crowdId < 0)
                {
                    agent.crowdId = idx;
                    idx++;
                    m_Agents[k] = agent;
                }
            }
            Debug.Assert(idx == m_PlanPathForAgent.Length);
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
                var queryCleanupJob = new QueryCleanupJob
                {
                    queryQueue = queue,
                    pathRequestIdForAgent = m_PathRequestIdForAgent
                };
                afterQueriesCleanup = queryCleanupJob.Schedule(afterQueriesCleanup);
            }
        }

        var pathNeededJob = new CheckPathNeededJob
        {
            agents = m_Agents,
            planPathForAgent = m_PlanPathForAgent,
            pathRequestIdForAgent = m_PathRequestIdForAgent,
            paths = m_AgentPaths.GetReadOnlyData()
        };
        var afterPathNeedChecked = pathNeededJob.Schedule(m_Agents.Length, 25, afterQueriesCleanup);

        var makeRequestsJob = new MakePathRequestsJob
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
            var resultsJob = new ApplyQueryResultsJob { queryQueue = queue, paths = m_AgentPaths.GetAllData() };
            afterPathsAdded = resultsJob.Schedule(afterPathsAdded);
        }

        var advance = new AdvancePathJob { agents = m_Agents, paths = m_AgentPaths.GetRangesData() };
        var afterPathsTrimmed = advance.Schedule(m_Agents.Length, 20, afterPathsAdded);

        var vel = new UpdateVelocityJob { agents = m_Agents, paths = m_AgentPaths.GetReadOnlyData() };
        var afterVelocitiesUpdated = vel.Schedule(m_Agents.Length, 15, afterPathsTrimmed);

        var move = new MoveLocationsJob { agents = m_Agents, dt = Time.deltaTime };
        var afterAgentsMoved = move.Schedule(m_Agents.Length, 10, afterVelocitiesUpdated);

        //var arrivalJob = new CheckArrivalToDestinationJob { agents = m_Agents };
        //afterAgentsMoved = arrivalJob.Schedule(afterAgentsMoved);

        AddDependency(afterAgentsMoved);

        // TODO: job safety for navmesh mutation
        // NavMeshManager.DidScheduleQueryJobs(afterAgentsMoved);
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
}
